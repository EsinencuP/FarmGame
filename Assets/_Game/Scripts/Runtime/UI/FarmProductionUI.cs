using System;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.World;
using MyLittleFarm.Gameplay.Building;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>One compact panel for all three single-batch workshops, owned by FarmMenuUI.</summary>
    public sealed class FarmProductionUI : MonoBehaviour
    {
        private BuildSystem _build;
        private PlayerController _player;
        private string _buildingId;
        private ProductionRecipe _selectedRecipe;
        private ProductionRecipe _styledRecipe;
        private Text _title, _input, _output, _state, _note;
        private Image _inputIcon, _outputIcon, _fill;
        private Button _start, _collect, _close;
        private Button _wheat, _corn;
        private float _nextRefresh;
        public RectTransform Panel => (RectTransform)transform;
        public Button CloseButton => _close;
        private void OnEnable() { GameEvents.InventoryChanged+=Refresh; }
        private void OnDisable() { GameEvents.InventoryChanged-=Refresh; _buildingId=null; }
        private void Update()
        { if(Time.unscaledTime>=_nextRefresh) { _nextRefresh=Time.unscaledTime+.1f; Refresh(); } }
        public void Open(string id)
        { _buildingId=id; _selectedRecipe=_build.RecipeFor(id); gameObject.SetActive(true); _note.text="Одна партия · следующая после сбора"; Refresh(); }
        public void Hide() => gameObject.SetActive(false);
        private bool CanReach() => _player!=null && _build.ProductionInReach(_buildingId,_player.transform.position);
        private void StartBatch()
        {
            if(_selectedRecipe==null || !CanReach()) { _note.text="Подойдите к постройке."; return; }
            _build.TryStartProduction(_buildingId,_selectedRecipe.Id,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),out var message);
            _note.text=message; Refresh();
        }
        private void CollectBatch()
        {
            var recipe=ProductionRecipe.Find(_build.Production?.Get(_buildingId)?.recipeId) ?? _selectedRecipe;
            if(recipe==null || !CanReach()) { _note.text="Подойдите к постройке."; return; }
            var ok=_build.TryCollectProduction(_buildingId,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),out var message);
            _note.text=message;
            if(ok) GameEvents.RaiseActionFeedback("harvest",_player.transform.position,recipe.OutputId,1);
            Refresh();
        }
        private void Refresh()
        {
            if(_build==null || _title==null) return;
            var building=_build.RecipeFor(_buildingId);
            _start.interactable=_collect.interactable=false;
            if(building==null) { _state.text="Постройка недоступна. Закройте окно."; return; }
            var service=_build.Production; var batch=service.Get(_buildingId);
            _selectedRecipe = ProductionRecipe.Find(batch?.recipeId) ?? _selectedRecipe ?? building;
            var recipe = _selectedRecipe;
            var isMill=building.BuildingId==ProductionRecipe.Mill.BuildingId;
            _wheat.gameObject.SetActive(isMill);
            _corn.gameObject.SetActive(isMill);
            if (isMill && _styledRecipe != recipe)
            {
                FarmUITheme.Style(_wheat, recipe == ProductionRecipe.Mill ? "primary" : "ghost");
                FarmUITheme.Style(_corn, recipe == ProductionRecipe.CornMill ? "primary" : "ghost");
                _styledRecipe = recipe;
            }
            _wheat.interactable = _corn.interactable = batch == null;
            var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var ready=batch!=null && now>=batch.readyAtUnixMs;
            var owned=service.Amount(recipe.InputId);
            _title.text=recipe.Name.ToUpperInvariant();
            _input.text=recipe.InputName+" × "+recipe.InputAmount+"\nВ инвентаре: "+owned;
            _output.text=recipe.OutputName+" × 1\nПродажа: "+recipe.SellPrice+" монет";
            _inputIcon.sprite=FarmUITheme.Sprite("Icons/"+FarmUITheme.ItemIcon(recipe.InputId));
            _outputIcon.sprite=FarmUITheme.Sprite("Icons/"+FarmUITheme.ItemIcon(recipe.OutputId));
            var progress=batch==null?0:Mathf.Clamp01((float)((double)(now-batch.startedAtUnixMs)/(batch.readyAtUnixMs-batch.startedAtUnixMs)));
            _fill.rectTransform.anchorMax=new Vector2(progress,1);
            if(batch==null) _state.text="Нет партии · время: "+FarmText.Duration(recipe.Seconds);
            else if(!ready) _state.text="В работе · "+Mathf.FloorToInt(progress*100)+"% · осталось "+FarmText.Duration((float)Math.Ceiling((batch.readyAtUnixMs-now)/1000d));
            else _state.text="Готово · "+recipe.OutputName+" × "+batch.outputAmount;
            var room=service.Amount(recipe.OutputId)<=int.MaxValue-1;
            if(ready && !room) _state.text+="\nИнвентарь полон · продукт сохранён здесь";
            _start.interactable=batch==null && owned>=recipe.InputAmount && CanReach();
            _collect.interactable=ready && room && CanReach();
            if(batch==null && owned<recipe.InputAmount) _state.text+="\nНедостаточно сырья";
        }
        private void SelectRecipe(ProductionRecipe recipe)
        {
            _selectedRecipe = recipe;
            _note.text = "Одна партия · следующая после сбора";
            Refresh();
        }
        public static FarmProductionUI Create(Transform parent,Font font,BuildSystem build)
        {
            var panel=FarmUITheme.Panel(parent,"ПРОИЗВОДСТВО",font,new Vector2(780,490));
            panel.gameObject.SetActive(false);
            var ui=panel.gameObject.AddComponent<FarmProductionUI>(); ui._build=build;
            ui._player=build.GetComponentInParent<GameBootstrap>().GetComponentInChildren<PlayerController>(true);
            ui._title=panel.GetChild(0).GetComponentInChildren<Text>();
            ui._inputIcon=FarmUITheme.Icon(panel,"crop_apple"); Place(ui._inputIcon.rectTransform,-220,-110,80,80);
            ui._outputIcon=FarmUITheme.Icon(panel,"crop_apple_juice"); Place(ui._outputIcon.rectTransform,220,-110,80,80);
            ui._input=FarmUITheme.Label(panel,"",font,24); Place(ui._input.rectTransform,-220,-180,310,72);
            ui._output=FarmUITheme.Label(panel,"",font,24); Place(ui._output.rectTransform,220,-180,310,72);
            var arrow=FarmUITheme.Label(panel,"→",font,36); Place(arrow.rectTransform,0,-125,70,60);
            ui._wheat=RecipeButton(panel,font,ProductionRecipe.Mill,-82,ui);
            ui._corn=RecipeButton(panel,font,ProductionRecipe.CornMill,82,ui);
            ui._state=FarmUITheme.Label(panel,"",font,24); Place(ui._state.rectTransform,0,-264,690,68);
            var track=FarmUITheme.Image(panel,"Progress track","Skin/slot"); Place(track.rectTransform,0,-222,650,18);
            ui._fill=FarmUITheme.Image(track.transform,"Progress"); ui._fill.color=new Color(.39f,.62f,.2f); FarmUITheme.Stretch(ui._fill.rectTransform);
            ui._note=FarmUITheme.Label(panel,"",font,20); Place(ui._note.rectTransform,0,-334,690,52);
            ui._start=FarmUITheme.Button(panel,"Запустить",font,"continue");
            ui._collect=FarmUITheme.Button(panel,"Забрать",font,"inventory","secondary");
            ui._close=FarmUITheme.Button(panel,"Закрыть",font,null,"ghost");
            Place((RectTransform)ui._start.transform,-242,-410,220,66);
            Place((RectTransform)ui._collect.transform,0,-410,220,66);
            Place((RectTransform)ui._close.transform,242,-410,220,66);
            ui._start.onClick.AddListener(ui.StartBatch); ui._collect.onClick.AddListener(ui.CollectBatch);
            return ui;
        }
        private static Button RecipeButton(RectTransform panel, Font font, ProductionRecipe recipe, float x, FarmProductionUI ui)
        {
            var button=FarmUITheme.Button(panel,recipe.InputName,font,null,"secondary");
            FarmUITheme.FitCompactButton(button,22);
            Place((RectTransform)button.transform,x,-73,150,39);
            button.onClick.AddListener(() => ui.SelectRecipe(recipe));
            button.gameObject.SetActive(false);
            return button;
        }
        private static void Place(RectTransform rect,float x,float y,float width,float height)
            => FarmUITheme.Place(rect,new Vector2(.5f,1),new Vector2(.5f,.5f),new Vector2(x,y),new Vector2(width,height));
    }
}
