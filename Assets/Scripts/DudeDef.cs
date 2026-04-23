using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewDudeDef", menuName = "Defs/Dude")]
public class DudeDef : ScriptableObject
{
    public Sprite idle;
    public Sprite jump;
    public Sprite dash;
    public Sprite beenHit;
    public Sprite death;
    public Sprite winner;
    public List<Sprite> attack;

    public Sprite dudeOnARockTitle;

    public List<AudioClip> gotTheRock;
    public List<AudioClip> attacks;
    public AudioClip knockedOffRock;
}
