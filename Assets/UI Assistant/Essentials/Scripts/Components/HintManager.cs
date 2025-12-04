using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/hint-management?authuser=0#h.yuzvo53hjlba")]
    public class HintManager : MonoBehaviour, IOptionListener
    {
        #region Variables
        [Tooltip("The first child under the Hint Manager that contains all other objects.")] public GameObject Visuals;
        [Tooltip("The layout that will be instantiated to contain a single Hint Context's entries.")] public CanvasGroup Template;
        [Tooltip("The Hint Snippet that will be instantiated to display a single Hint Context entry.")] public HintSnippet HintSnippet;
        [Tooltip("An optional Boolean type Option Entry that can enable users to hide hints via settings.")] public OptionEntry OptionEntry;
        [Tooltip("The time it takes for a Hint Context's entries to appear and disappear.")] public float TransitionTime;
        [Space]
        [Tooltip("Unity Events to be invoked when the number of Hint Contexts becomes 1.")] public UnityEvent OnShow;
        [Tooltip("Unity Events to be invoked when Hint Contexts replace each other.")] public UnityEvent OnSwap;
        [Tooltip("Unity Events to be invoked when the number of Hint Contexts becomes 0.")] public UnityEvent OnHide;

        public static HintManager Instance;
        CanvasGroup ActiveContainer;
        readonly static List<HintContext> HintContextStack = new();
        readonly AnimationCurve TransitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        #endregion

        #region Function
        public void OnOptionBoolValueChanged(bool value)
        {
            Visuals.SetActive(value);
        }

        void Start()
        {
            if (OptionEntry != null) Visuals.SetActive(OptionEntry.BoolValue);
            Template.gameObject.SetActive(false);
        }
        void OnEnable()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                if (OptionEntry != null) OptionEntry.Add(this);
            }
            else Destroy(gameObject);
        }

        /// <summary>
        /// Adds a Hint Context to the stack, and displays Hint Snippets accordingly.
        /// </summary>
        /// <param name="hintContext">The Hint Context to be displayed.</param>
        public static void ShowHints(HintContext hintContext)
        {
            HintContextStack.Add(hintContext);
            Instance.CreateHintSnippets();
            Instance.HandleEvents();
        }

        /// <summary>
        /// Removes a Hint Context from the stack.
        /// </summary>
        /// <param name="hintContext">The Hint Context to be removed.</param>
        public static void RemoveHints(HintContext hintContext)
        {
            bool wasActive = hintContext == ActiveHintContext;
            HintContextStack.Remove(hintContext);
            if (wasActive)
            {
                if (HintContextStack.Count == 0) Instance.RemoveHintSnippets();
                else Instance.CreateHintSnippets();
            }
            Instance.HandleEvents();
        }

        void CreateHintSnippets()
        {
            RemoveHintSnippets();

            ActiveContainer = Instantiate(Instance.Template, Instance.Template.transform.parent);
            ActiveContainer.gameObject.SetActive(true);
            for (int i = 0; i < ActiveHintContext.Entries.Count; i++)
            {
                HintSnippet hintSnippet = Instantiate(Instance.HintSnippet, Instance.ActiveContainer.transform);
                hintSnippet.SetSnippet(i);
            }
            StartCoroutine(Instance.FadeContainer(false));
        }
        void RemoveHintSnippets()
        {
            if (ActiveContainer != null)
            {
                if (TransitionTime > 0) StartCoroutine(FadeContainer(true));
                else Destroy(ActiveContainer.gameObject);
            }
        }
        void HandleEvents()
        {
            if (HintContextStack.Count == 0) OnHide.Invoke();
            if (HintContextStack.Count == 1) OnShow.Invoke();
            else OnSwap.Invoke();
        }
        IEnumerator FadeContainer(bool fadeOut)
        {
            CanvasGroup canvasGroup = ActiveContainer;
            float currentTime = 0;
            float startAlpha = fadeOut ? 1 : 0;
            float targetAlpha = 1 - startAlpha;
            bool canvasAvailable = true;

            while (currentTime < TransitionTime)
            {
                canvasAvailable = canvasGroup != null;
                if (canvasAvailable)
                {
                    currentTime += Time.deltaTime;
                    float t = TransitionCurve.Evaluate(currentTime / TransitionTime);
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                }

                yield return null;
            }

            if (fadeOut && canvasAvailable) Destroy(canvasGroup.gameObject);
        }
        #endregion

        #region Helpers
        public static HintContext ActiveHintContext => HintContextStack[^1];
        #endregion
    }
}