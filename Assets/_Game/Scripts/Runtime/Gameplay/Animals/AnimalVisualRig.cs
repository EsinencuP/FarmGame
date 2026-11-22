using UnityEngine;

namespace MyLittleFarm.Gameplay.Animals
{
    /// <summary>Saved visual metrics and generic-rig foot contacts, independent of production/save data.</summary>
    public sealed class AnimalVisualRig : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Bounds pickBounds;
        [SerializeField] private Transform[] footBones, footContacts;
        [SerializeField] private float gaitSpeed = .4f, moveSpeed = .18f, badgeHeight = .8f;
        public Animator Animator => animator;
        public Bounds PickBounds => pickBounds;
        public Transform[] FootBones => footBones;
        public Transform[] FootContacts => footContacts;
        public float GaitSpeed => gaitSpeed;
        public float MoveSpeed => moveSpeed;
        public float BadgeHeight => badgeHeight;
        public void Configure(Animator animation, Bounds bounds, Transform[] bones, Transform[] contacts,
            float referenceSpeed, float speed, float height)
        { animator=animation;pickBounds=bounds;footBones=bones;footContacts=contacts;
            gaitSpeed=referenceSpeed;moveSpeed=speed;badgeHeight=height; }
    }
}
