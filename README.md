# My Little Farm

В Unity откройте [MainMenu](Assets/_Game/Scenes/MainMenu.unity) для запуска с титульного экрана или [Prototype](Assets/_Game/Scenes/Prototype.unity) для работы с фермой. Версия редактора указана в [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt).

## Где что лежит

| Папка | Содержимое |
|---|---|
| [Scripts/Runtime](Assets/_Game/Scripts/Runtime) | `Core` — запуск, ввод, сетка и сохранения. `Gameplay` — фермерство, животные, строительство, экономика и мир. `UI` — экраны и панель F1. `Audio` — воспроизведение звука. |
| [Scripts/Editor](Assets/_Game/Scripts/Editor) | Инструменты Unity для сцен, мира, арта, интерфейса и игровых систем. |
| [Content](Assets/_Game/Content) | Модели и префабы игры, персонаж, культуры, деревья, звук, окружение и интерфейс. Комикс в `UI/Comics`, данные клеток в `World/Maps`, оформленный ландшафт в `World/Landscape`. |
| [Resources](Assets/_Game/Resources) | Ресурсы, загружаемые по имени в игре. Ключи загрузки начинаются после папки `Resources`. |
| [ThirdParty](Assets/ThirdParty) | Импортированные пакеты по поставщикам, включая исходники моделей, текстур и анимаций. |
| [_Recovery](Assets/_Recovery) | Резервные сцены для восстановления. |
| [Tests](Assets/_Game/Tests) · [Verification](Verification) | Сценарии EditMode и PlayMode, отчёты и материалы проверок. |
| [Docs](Docs) · [Tools](Tools) | Документы проекта и вспомогательные инструменты. GDD, руководство Unity и исходный план находятся в [Docs/Design](Docs/Design). |

Порядок проверок описан в [AGENTS.md](AGENTS.md). Текущие этапы и решения собраны в [roadmap](Docs/MyLittleFarm_RoadToRelease.md) и [контексте проекта](Docs/AI/UnityProjectContext.md).
