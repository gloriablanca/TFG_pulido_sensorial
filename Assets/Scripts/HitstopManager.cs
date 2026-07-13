using System.Collections;
using UnityEngine;

public class HitstopManager : MonoBehaviour
{
    public static HitstopManager Instance;

    [SerializeField] Canvas colorCanvas;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    void Start()
    {
        colorCanvas.gameObject.SetActive(false);
    }

    public void CallHitstop(float delay, int frames)
    {
        StartCoroutine(HitstopCoroutine(delay, frames));
    }

    IEnumerator HitstopCoroutine(float delay, int frames)
    {
        colorCanvas.gameObject.SetActive(true);
        yield return new WaitForSeconds(delay);

        Time.timeScale = 0f;
        for (int i = 0; i < frames; i++)
            yield return new WaitForEndOfFrame();

        Time.timeScale = 1f;
        colorCanvas.gameObject.SetActive(false);
    }
}
