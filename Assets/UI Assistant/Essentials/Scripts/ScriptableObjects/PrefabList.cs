using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/prefab-list?authuser=0")]
    public class PrefabList : ScriptableObject
    {
#if UNITY_EDITOR

        #region Variables
        /// <summary>
        /// If a prefab is instantiated without a parent Canvas, one will be created for it automatically.
        /// </summary>
        [HideInInspector] public bool AutoCreateCanvas;

        /// <summary>
        /// A list of prefabs for basic UI elements.
        /// </summary>
        [HideInInspector] public List<Prefab> EssentialPrefabs = new()
        {
            new("Button"), new("Dropdown"), new("Input Field"), new("Option Cycler"), new("Scrollbar"), new("Slider"),new("Toggle")
        };

        /// <summary>
        /// A list of prefabs for custom UI elements.
        /// </summary>
        [HideInInspector] public List<Prefab> CustomPrefabs = new();
        #endregion

        #region Classes
        [System.Serializable] public class Prefab
        {
            public string Name;
            public GameObject GameObject;

            public Prefab()
            {
                Name = "New Prefab";
                GameObject = null;
            }
            public Prefab(string name)
            {
                Name = name;
                GameObject = null;
            }
        }
        #endregion

#endif
    }
}