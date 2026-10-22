using UnityEngine;

namespace MyLittleFarm.Gameplay.Building
{
    /// <summary>World text is plain text and constrained to the authored board, including Cyrillic.</summary>
    public sealed class FarmSign : MonoBehaviour
    {
        [SerializeField] private TextMesh front;
        [SerializeField] private TextMesh back;
        [SerializeField] private Material textMaterial;
        [SerializeField] private float boardWidth=.82f;
        private Material _runtimeMaterial;
        private void OnEnable(){Font.textureRebuilt+=RefreshFont;RefreshFont(null);}
        private void OnDisable()
        {
            Font.textureRebuilt-=RefreshFont;
            if(_runtimeMaterial==null)return;
            foreach(var text in new[]{front,back})if(text!=null)text.GetComponent<MeshRenderer>().sharedMaterial=textMaterial;
            if(Application.isPlaying)Destroy(_runtimeMaterial);else DestroyImmediate(_runtimeMaterial);
            _runtimeMaterial=null;
        }
        private void RefreshFont(Font font)
        {
            if(textMaterial==null||front==null||front.font==null||(font!=null&&font!=front.font))return;
            if(Application.isPlaying&&_runtimeMaterial==null)_runtimeMaterial=new Material(textMaterial){hideFlags=HideFlags.DontSave};
            var material=Application.isPlaying?_runtimeMaterial:textMaterial;
            material.mainTexture=front.font.material.mainTexture;
            foreach(var text in new[]{front,back})if(text!=null)text.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        public void SetText(string value)
        {
            value=FarmDecorationCatalog.CleanText(value);
            if(string.IsNullOrEmpty(value))value="Моя ферма";
            var length=value.Length;
            if(length>22)
            {
                var split=value.LastIndexOf(' ',Mathf.Min(24,length-1));
                if(split<8)split=Mathf.Min(24,length);
                value=value.Substring(0,split).TrimEnd()+"\n"+value.Substring(split).TrimStart();
            }
            foreach(var text in new[]{front,back})
            {
                if(text==null)continue;
                text.richText=false;text.text=value;
                text.font.RequestCharactersInTexture(value,text.fontSize,FontStyle.Normal);
                text.characterSize=1f;
                var size=text.GetComponent<MeshRenderer>().localBounds.size;
                text.characterSize=Mathf.Min((boardWidth-.12f)/Mathf.Max(.001f,size.x),.42f/Mathf.Max(.001f,size.y));
            }
            RefreshFont(null);
        }
    }
}
