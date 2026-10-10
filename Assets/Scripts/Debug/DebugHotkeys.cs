using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Dev-only keyboard shortcuts. Drop on any object in the scene; it only calls into the game's public API.
// Defender keys pick the defender by launcher index; held pose keys preview a pose on the defender's dude.
// Poses are set once on press and reset to Idle on release, so anything else that changes the pose
// mid-hold (e.g. an attack timing out) wins.
public class DebugHotkeys : MonoBehaviour 
{
    [Serializable]
    public struct PoseKey
    {
        public Key key;
        public DudeState pose;
    }

    [Tooltip("Index i sets GameController.launchers[i] as defender.")]
    public List<Key> defenderKeys = new() { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };

    [Tooltip("Avoid W A S D: they're the players' launch buttons in InputSystem_Actions.")]
    public List<PoseKey> poseKeys = new()
    {
        new PoseKey { key = Key.R, pose = DudeState.Jump },
        new PoseKey { key = Key.T, pose = DudeState.Dash },
        new PoseKey { key = Key.Y, pose = DudeState.BeenHit },
        new PoseKey { key = Key.U, pose = DudeState.Attack },
        new PoseKey { key = Key.I, pose = DudeState.Death },
        new PoseKey { key = Key.O, pose = DudeState.Winner },
        new PoseKey { key = Key.P, pose = DudeState.Ultimate },
    };

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void Update()
    {
        var kb = Keyboard.current;
        var gc = GameController.Instance;
        if (kb == null || gc == null || gc.launchers == null) return;

        for (int i = 0; i < defenderKeys.Count && i < gc.launchers.Count; i++)
            if (kb[defenderKeys[i]].wasPressedThisFrame && gc.launchers[i] != null)
                gc.SetDefender(gc.launchers[i]);

        Dude dude = gc.CurrentDefender != null ? gc.CurrentDefender.DefendDude : null;
        if (dude == null) return;

        foreach (var pk in poseKeys)
        {
            if (kb[pk.key].wasPressedThisFrame) SetPose(dude, pk.pose);
            else if (kb[pk.key].wasReleasedThisFrame) SetPose(dude, DudeState.Idle);
        }
    }

    static void SetPose(Dude dude, DudeState pose)
    {
        dude.state = pose;
        dude.UpdateSprite();
    }
#endif
}
