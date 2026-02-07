using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    public class Interactable : MonoBehaviour
    {
        public InteractionSystem.InteractionType interactionType;
        public string targetId;
        public string promptText;
        public float interactionRange = 3f;
        public bool requiresLineOfSight = true;
        public List<string> requiredItems = new List<string>();
    }
}
