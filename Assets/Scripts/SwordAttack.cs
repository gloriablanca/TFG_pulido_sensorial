using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SwordAttack : MonoBehaviour
{
    [Header("Pivote espada")]
    [SerializeField] private Transform pivoteEspada;

    [Header("Animación swing")]
    [SerializeField, Range(0.1f, 1f)]  private float duracionSwing = 0.3f;
    //grados que recorre el pivote en el arco (eje Y local del pivote)
    [SerializeField, Range(30f, 180f)] private float arcoSwing     = 90f;

    [Header("Cooldown")]
    [SerializeField, Range(0f, 2f)] private float cooldown = 0.5f;

    [Header("Detección golpe")]
    [SerializeField, Range(0.5f, 6f)]  private float radioDeteccion  = 2f;
    //ángulo total del cono frontal (por ej 120 = 60° a cada lado del forward del Player)
    [SerializeField, Range(10f, 360f)] private float anguloDeteccion = 120f;
    [SerializeField]                   private LayerMask capaEnemigos;

    [Header("Trail")]
    [SerializeField] GameObject trailParent;

    //sonido capa 4
    [Header("Sonido capa 4")]
    [Tooltip("se reproduce al iniciar el ataque (no al impactar)")]
    [SerializeField] private AudioSource sfxWhoosh;

    private Quaternion reposo; //rotación local del pivote al iniciar
    private bool swingActivo;
    private readonly HashSet<int> golpeadosEnEsteSwing = new HashSet<int>();

    void Start()
    {
        if (pivoteEspada == null)
        {
            Debug.LogError("[SwordAttack] asigna el Transform del PivoteEspada en el inspector", this);
            enabled = false;
            return;
        }
        reposo = pivoteEspada.localRotation;

        InicializarTrails();
    }

    void Update()
    {
        if (swingActivo) return;
        if (Input.GetButtonDown("Fire1") || Input.GetKeyDown(KeyCode.Space))
            StartCoroutine(CorrutinaSwing());
    }

    IEnumerator CorrutinaSwing()
    {
        ToggleTrails(true);
        ReproducirSonido(sfxWhoosh);
        swingActivo = true;
        golpeadosEnEsteSwing.Clear();

        Quaternion pico  = reposo * Quaternion.Euler(0f, -arcoSwing, 0f);
        float      mitad = duracionSwing * 0.5f;
        float      t     = 0f;

        //ida
        while (t < mitad)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / mitad);
            pivoteEspada.localRotation = Quaternion.Slerp(
                reposo, pico, Mathf.SmoothStep(0f, 1f, u));
            DetectarImpactos();
            yield return null;
        }
        pivoteEspada.localRotation = pico;

        t = 0f;

        //vuelta
        while (t < mitad)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / mitad);
            pivoteEspada.localRotation = Quaternion.Slerp(
                pico, reposo, Mathf.SmoothStep(0f, 1f, u));
            DetectarImpactos();
            yield return null;
        }
        pivoteEspada.localRotation = reposo;

        yield return new WaitForSeconds(cooldown);
        swingActivo = false;
        ToggleTrails(false);
    }

    void DetectarImpactos()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, radioDeteccion, capaEnemigos);
        foreach (Collider col in cols)
        {
            int id = col.gameObject.GetInstanceID();
            if (golpeadosEnEsteSwing.Contains(id)) continue;

            Vector3 dir = col.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) continue;
            if (Vector3.Angle(transform.forward, dir) > anguloDeteccion * 0.5f) continue;

            golpeadosEnEsteSwing.Add(id);

            if (col.TryGetComponent(out Enemy enemigo))
                enemigo.TakeHit(transform.position);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radioDeteccion);
        Gizmos.color = Color.yellow;
        float semi = anguloDeteccion * 0.5f;
        Gizmos.DrawRay(transform.position,
            Quaternion.Euler(0f, -semi, 0f) * transform.forward * radioDeteccion);
        Gizmos.DrawRay(transform.position,
            Quaternion.Euler(0f,  semi, 0f) * transform.forward * radioDeteccion);
    }

    void InicializarTrails()
    {
        if (trailParent == null) return;
        foreach (TrailRenderer trail in trailParent.GetComponentsInChildren<TrailRenderer>(true))
        {
            trail.enabled  = true;
            trail.emitting = false;
            trail.Clear();
        }
    }

    void ToggleTrails(bool emitir)
    {
        if (trailParent == null) return;

        bool capaActiva = PolishManager.Instance != null && PolishManager.Instance.espadaExpresiva;
        bool emitirReal = emitir && capaActiva;

        foreach (TrailRenderer trail in trailParent.GetComponentsInChildren<TrailRenderer>(true))
        {
            if (emitirReal) trail.Clear();
            trail.emitting = emitirReal;
        }
    }

    void ReproducirSonido(AudioSource source)
    {
        if (PolishManager.Instance == null || !PolishManager.Instance.sonido) return;

        if (source == null)
        {
            Debug.LogWarning("[SwordAttack] sfxWhoosh sin asignar (AudioSource null). Arrástralo en el Inspector.", this);
            return;
        }
        if (source.clip == null)
        {
            Debug.LogWarning($"[SwordAttack] El AudioSource '{source.name}' no tiene Clip asignado.", source);
            return;
        }

        source.Play();
    }
}
