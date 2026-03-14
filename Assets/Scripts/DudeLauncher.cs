using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class DudeLauncher : MonoBehaviour
{
    public Dude dudePrefab;

    public List<Transform> dudeBases;
    public InputActionReference launchAction;

    void OnEnable()
    {
        if (launchAction != null)
        {
            launchAction.action.Enable();
            launchAction.action.performed += OnLaunchPerformed;
        }
    }

    void OnDisable()
    {
        if (launchAction != null)
        {
            launchAction.action.performed -= OnLaunchPerformed;
        }
    }

    void OnLaunchPerformed(InputAction.CallbackContext ctx)
    {
        Launch();
    }

    void Launch()
    {
        if (dudePrefab == null || TheRock.Instance == null || dudeBases == null || dudeBases.Count == 0) return;

        Transform source = dudeBases[Random.Range(0, dudeBases.Count)];
        Dude dude = Instantiate(dudePrefab, source.position, Quaternion.identity);
        LaunchedDude launched = dude.gameObject.AddComponent<LaunchedDude>();
        launched.source = source;
        launched.target = TheRock.Instance.Hilltop;
    }
}
