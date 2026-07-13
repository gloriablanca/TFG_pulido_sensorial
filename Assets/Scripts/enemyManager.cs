using UnityEngine;

public class enemyManager : MonoBehaviour
{
    [SerializeField] GameObject wavePrefab;
    [SerializeField] GameObject currentWave;

    void Update()
    {
        //E para reiniciar la oleada de enemigos
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (currentWave != null)
                Destroy(currentWave);

            currentWave = Instantiate(wavePrefab, transform);
        }
    }
}
