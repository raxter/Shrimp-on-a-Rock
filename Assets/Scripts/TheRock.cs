using UnityEngine;

public class TheRock : MonoBehaviour
{
    public static TheRock Instance { get; private set; }

    [Tooltip("Authoring stand-in for the dude on the rock. Switched off on Awake; the real defenders are spawned by each launcher.")]
    public GameObject hilltopPreview;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("More than one instance of theRock exists");
            return;
        }
        Instance = this;

        if (hilltopPreview != null) hilltopPreview.SetActive(false);
    }
}
