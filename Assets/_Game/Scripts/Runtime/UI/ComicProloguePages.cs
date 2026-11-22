using UnityEngine;

namespace MyLittleFarm.UI
{
    [CreateAssetMenu(fileName = "ComicPrologue", menuName = "My Little Farm/Comic Prologue")]
    public sealed class ComicProloguePages : ScriptableObject
    {
        [SerializeField] private Texture2D[] pages;

        internal Texture2D[] Pages => pages;
    }
}
