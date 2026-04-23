using System.Collections.Generic;
using UnityEngine;

public class BubbleAnimator : MonoBehaviour
{
    public enum BubbleState { Nothing, Bubble, Popped }

    public FourWayImage target;
    public Sprite nothingSprite;
    public List<Sprite> growSprites;
    public List<Sprite> popSprites;
    public float fps = 30f;

    public BubbleState state = BubbleState.Nothing;

    void Start()
    {
        if (state == BubbleState.Nothing)
            SetFrame(nothingSprite);
    }

    public void ResetToNothing()
    {
        _playing = null;
        state = BubbleState.Nothing;
        SetFrame(nothingSprite);
    }

    private List<Sprite> _playing;
    private BubbleState _endState;
    private int _frame;
    private float _timer;

    public void Bubble()
    {
        StartPlay(growSprites, BubbleState.Bubble);
    }

    public void Pop()
    {
        StartPlay(popSprites, BubbleState.Popped);
    }

    void StartPlay(List<Sprite> frames, BubbleState endState)
    {
        _playing = frames;
        _endState = endState;
        _frame = 0;
        _timer = 0f;

        if (_playing == null || _playing.Count == 0 || target == null)
        {
            state = endState;
            _playing = null;
            return;
        }

        SetFrame(_playing[0]);
    }

    void Update()
    {
        if (_playing == null) return;

        float interval = fps > 0f ? 1f / fps : 0f;
        if (interval <= 0f) return;

        _timer += Time.deltaTime;
        if (_timer < interval) return;

        _timer -= interval;
        _frame++;
        if (_frame >= _playing.Count)
        {
            state = _endState;
            _playing = null;
            return;
        }

        SetFrame(_playing[_frame]);
    }

    void SetFrame(Sprite s)
    {
        if (target == null) return;
        target.sprite = s;
        target.RefreshSprite();
    }
}
