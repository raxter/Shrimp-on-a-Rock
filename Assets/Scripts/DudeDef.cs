using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewDudeDef", menuName = "Defs/Dude")]
public class DudeDef : ScriptableObject
{
    public Sprite idle;
    public Sprite jump;
    public Sprite dash;
    public Sprite beenHit;
    public List<Sprite> attack;
}
