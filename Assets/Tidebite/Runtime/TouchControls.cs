using Tidebite.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Tidebite
{
    public sealed class TouchControls : MonoBehaviour
    {
        public V2 Move { get; private set; }
        private bool bite, dash;
        private VirtualStick stick;
        private PressAction biteButton;
        private bool enabledInput;
        public void Bind(VirtualStick inputStick) => stick = inputStick;
        public void BindBite(PressAction button)=>biteButton=button;
        public void SetActive(bool active) { enabledInput = active; ResetInput(); }
        public void Bite() { if (enabledInput) bite = true; }
        public void Dash() { if (enabledInput) dash = true; }
        public void ResetInput() { Move = default; bite = dash = false; stick?.ResetStick(); }
        private void OnApplicationFocus(bool focus) { if (!focus) ResetInput(); }
        public FishCommand Read()
        {
            if (!enabledInput) return default;
            Vector2 movement = stick != null ? stick.Value : Vector2.zero;
            var keys = Keyboard.current;
            if (keys != null)
            {
                Vector2 keyboard = new Vector2(
                    (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0) - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0),
                    (keys.wKey.isPressed || keys.upArrowKey.isPressed ? 1 : 0) - (keys.sKey.isPressed || keys.downArrowKey.isPressed ? 1 : 0));
                if (keyboard.sqrMagnitude > .01f) movement = keyboard.normalized;
                bite |= keys.spaceKey.isPressed || keys.jKey.isPressed;
                dash |= keys.leftShiftKey.wasPressedThisFrame || keys.kKey.wasPressedThisFrame;
            }
            Move = new V2(movement.x, movement.y);
            bite |= biteButton!=null && biteButton.Held;
            var command = new FishCommand(Move, bite, dash); bite = dash = false;
            return command;
        }
    }

    public sealed class VirtualStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Vector2 Value { get; private set; }
        public RectTransform Knob;
        private int pointer = int.MinValue;
        public void OnPointerDown(PointerEventData data)
        {
            if (pointer != int.MinValue) return;
            pointer = data.pointerId; OnDrag(data);
        }
        public void OnDrag(PointerEventData data)
        {
            if (data.pointerId != pointer) return;
            var rect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, data.position, data.pressEventCamera, out var local);
            float radius = rect.rect.width * .32f;
            Value = Vector2.ClampMagnitude(local / radius, 1);
            if (Value.magnitude < .12f) Value = Vector2.zero;
            Knob.anchoredPosition = Value * radius;
        }
        public void OnPointerUp(PointerEventData data) { if (pointer == data.pointerId) ResetStick(); }
        public void ResetStick() { pointer = int.MinValue; Value = Vector2.zero; if (Knob != null) Knob.anchoredPosition = Vector2.zero; }
        private void OnDisable() => ResetStick();
    }

    public sealed class PressAction : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public System.Action Press;
        private int pointer = int.MinValue;
        public bool Held=>pointer!=int.MinValue;
        public void OnPointerDown(PointerEventData data)
        {
            if (pointer != int.MinValue) return;
            pointer = data.pointerId; transform.localScale = Vector3.one * .92f; Press?.Invoke();
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (pointer != data.pointerId) return;
            pointer = int.MinValue; transform.localScale = Vector3.one;
        }
        private void OnDisable() { pointer = int.MinValue; transform.localScale = Vector3.one; }
    }
}
