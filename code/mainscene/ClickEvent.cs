using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

using System;
public class ClickEvent
{
    public class CardClickHandler                                              //带单双击
        : MonoBehaviour, IPointerClickHandler                 
    {
        public float doubleClickThreshold = 0.3f;
        public Action OnSingleClicked;
        public Action OnDoubleClicked;

        private float lastClickTime = 0f;
        private Coroutine clickCoroutine;

        public void OnPointerClick(PointerEventData eventData)
        {
            float currentTime = Time.time;
            if (currentTime - lastClickTime < doubleClickThreshold)
            {
                if (clickCoroutine != null)
                    StopCoroutine(clickCoroutine);
                OnDoubleClicked?.Invoke();
                lastClickTime = 0f;
            }
            else
            {
                if (clickCoroutine != null)
                    StopCoroutine(clickCoroutine);
                clickCoroutine = StartCoroutine(DelayedSingleClick());
                lastClickTime = currentTime;
            }
        }

        private IEnumerator DelayedSingleClick()
        {
            yield return new WaitForSeconds(doubleClickThreshold);
            OnSingleClicked?.Invoke();
            clickCoroutine = null;
        }
    }                  

    public class SimpleClickHandler                                            //单击事件
        : MonoBehaviour, IPointerClickHandler
    {
        public Action OnClicked;
        public void OnPointerClick(PointerEventData eventData)
        {
            OnClicked?.Invoke();
        }
    }
}
