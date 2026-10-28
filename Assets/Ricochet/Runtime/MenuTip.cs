// U.RICO-T52: a Start screen row's hover message. The original's setup buttons each drew their message at the bottom of the
// titlescreen room while the pointer was over them; here a row shows its message while the pointer or the keyboard's focus
// is on it (on any of its parts: pointer events reach every parent), and clears it when they leave.
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Ricochet
{
    public sealed class MenuTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        /// <summary>The tip's name (one of MenuFlow.TipNames).</summary>
        public string Name;

        /// <summary>Shows a tip by name (null: none).</summary>
        public Action<string> Show;

        public void OnPointerEnter(PointerEventData eventData) => Show?.Invoke(Name);

        public void OnPointerExit(PointerEventData eventData) => Show?.Invoke(null);

        public void OnSelect(BaseEventData eventData) => Show?.Invoke(Name);

        public void OnDeselect(BaseEventData eventData) => Show?.Invoke(null);
    }
}
