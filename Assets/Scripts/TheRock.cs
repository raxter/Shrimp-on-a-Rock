using UnityEngine;

public class TheRock : MonoBehaviour
{
    public static TheRock Instance { get; private set; }

    [SerializeField] private Transform hilltop;
    public Transform Hilltop => hilltop;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("More than one instance of theRock exists");
            return;
        }
        Instance = this;
    }
}
