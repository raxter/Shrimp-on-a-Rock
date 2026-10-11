using UnityEngine;

// A "press the button" prompt that shows its pressed sprite while the player's button is held. Set Pressed each frame.
public class PressPrompt : MonoBehaviour
{
    public FourWayImage image;
    public Sprite raisedSprite;
    public Sprite pressedSprite;

    private bool? _pressed;

    public bool Pressed
    {
        set
        {
            if (_pressed == value || image == null) return;
            _pressed = value;
            image.sprite = value ? pressedSprite : raisedSprite;
            image.RefreshSprite();
        }
    }
}
