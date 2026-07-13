using UnityEngine;
using System.Collections;

public class EnemyReaction : MonoBehaviour
{
    [Header("Referencia al Visual (lo que se deforma)")]
    [SerializeField] private Transform visual;

    //golpe normal
    [Header("Knockback")]
    [Tooltip("Distancia total que retrocede")]
    [SerializeField, Range(0f, 5f)] private float distanciaKnockback = 1.2f;
    [Tooltip("Cuánto dura el empuje (más corto = más seco)")]
    [SerializeField, Range(0.02f, 1f)] private float duracionKnockback  = 0.18f;

    [Header("Squash & Stretch NORMAL")]
    [Tooltip("sube Y, adelgaza XZ")]
    [SerializeField, Range(0f, 0.8f)] private float intensidadStretch  = 0.25f;
    [Tooltip("baja Y, ensancha XZ")]
    [SerializeField, Range(0f, 0.8f)] private float intensidadSquash   = 0.25f;
    [Tooltip("Duración total de la animación de escala + inclinación")]
    [SerializeField, Range(0.05f, 1.5f)] private float duracionEscala     = 0.45f;
    [Tooltip("Fuerza del rebote final (overshoot). Compartido por escala e inclinación")]
    [SerializeField, Range(0f, 3f)] private float fuerzaOvershoot    = 1.3f;

    [Header("Inclinación tipo bolo")]
    [Tooltip("Ángulo máximo de inclinación hacia la dirección del empuje")]
    [SerializeField, Range(0f, 60f)] private float anguloInclinacion  = 12f;

    //golpe mortal
    [Header("Muerte")]
    [Tooltip("Ángulo de caída hasta quedar tumbado. 90° = en el suelo. No se reendereza")]
    [SerializeField, Range(0f, 120f)] private float anguloMuerte             = 90f;
    [Tooltip("Duración total de la muerte. Mayor que el golpe normal para darle dramatismo")]
    [SerializeField, Range(0.1f, 2.5f)] private float duracionMuerte           = 0.8f;
    [Tooltip("Distancia de knockback al morir. Mayor que el golpe normal (sale más lejos)")]
    [SerializeField, Range(0f, 10f)] private float distanciaKnockbackMuerte = 3f;
    [Tooltip("Duración del empuje de la muerte ( ≤ 'duracionMuerte')")]
    [SerializeField, Range(0.02f, 1.5f)] private float duracionKnockbackMuerte  = 0.35f;

    private const float FRACCION_SQUASH = 0.30f;

    private Vector3 escalaOriginal = Vector3.one;        
    private Quaternion rotacionOriginal = Quaternion.identity;
    private Coroutine rutinaActiva;                          
    private bool muriendo;                              

    void Awake()
    {
        if (visual == null)
        {
            visual = transform.Find("Visual");
            if (visual == null)               
            {
                foreach (Transform t in GetComponentsInChildren<Transform>(true))
                    if (t.name == "Visual") { visual = t; break; }
            }
            if (visual == null)
                Debug.LogWarning("[EnemyReaction] No encuentro 'Visual', la deformación/inclinación se omitirá y " +
                                 "el knockback sí funcionará", this);
        }

        if (visual != null)
        {
            escalaOriginal = visual.localScale;
            rotacionOriginal = visual.localRotation;
        }
    }

    // si el enemigo se reactiva tras morir, vuelve a su pose original y se resetea el estado
    void OnEnable()
    {
        muriendo = false;
        if (visual != null)
        {
            visual.localScale = escalaOriginal;
            visual.localRotation = rotacionOriginal;
        }
    }


    public bool Reaccionar(Vector3 attackerPosition, bool esCritico)
    {
        if (PolishManager.Instance == null || !PolishManager.Instance.enemigoReacciona) return false;

        if (muriendo) return true;

        Vector3 push = transform.position - attackerPosition;
        push.y = 0f;
        push = push.sqrMagnitude > 0.0001f ? push.normalized : -transform.forward;

        if (rutinaActiva != null)
        {
            StopCoroutine(rutinaActiva);
            if (visual != null)
            {
                visual.localScale = escalaOriginal;
                visual.localRotation = rotacionOriginal;
            }
        }

        if (esCritico)
        {
            rutinaActiva = StartCoroutine(RutinaMuerte(push));
            return true;
        }

        //normal
        rutinaActiva = StartCoroutine(RutinaReaccion(push));
        return false;
    }

    private IEnumerator RutinaMuerte(Vector3 pushWorld)
    {
        muriendo = true;

        Vector3 worldTiltAxis = EjeInclinacionMundo(pushWorld);

        //sin stretch ni squash, escala original x si acaso
        if (visual != null) visual.localScale = escalaOriginal;

        float t = 0f;
        float easedPrev = 0f;

        while (t < duracionMuerte)
        {
            //NO TOCAR
            yield return null;
            t += Time.deltaTime;
            if (duracionKnockbackMuerte > 0f)
            {
                float kp = Mathf.Clamp01(t / duracionKnockbackMuerte);
                float eased = 1f - Mathf.Pow(1f - kp, 2f);
                transform.position += pushWorld * ((eased - easedPrev) * distanciaKnockbackMuerte);
                easedPrev = eased;
            }


            float m = Mathf.Clamp01(t / duracionMuerte);
            float caida = 1f - Mathf.Pow(1f - m, 2f);
            AplicarInclinacion(anguloMuerte * caida, worldTiltAxis);
        }

        rutinaActiva = null;
        if (TryGetComponent(out Enemy enemy)) enemy.DesaparecerAlMorir();
        else gameObject.SetActive(false);
    }

    //reacción normal
    private IEnumerator RutinaReaccion(Vector3 pushWorld)
    {
        Vector3 poseStretch = PoseStretch(intensidadStretch);

        Vector3 poseSquash = new Vector3(
            escalaOriginal.x * (1f + intensidadSquash * 0.5f),
            escalaOriginal.y * (1f - intensidadSquash),
            escalaOriginal.z * (1f + intensidadSquash * 0.5f));

        Vector3 worldTiltAxis = EjeInclinacionMundo(pushWorld);

        if (visual != null) visual.localScale = poseStretch;

        float t = 0f;
        float easedPrev = 0f;
        float maxDur = Mathf.Max(duracionKnockback, duracionEscala);

        while (t < maxDur)
        {
            yield return null;

            if (muriendo) yield break;

            t += Time.deltaTime;

            if (duracionKnockback > 0f)
            {
                float kp = Mathf.Clamp01(t / duracionKnockback);
                float eased = 1f - Mathf.Pow(1f - kp, 2f);
                transform.position += pushWorld * ((eased - easedPrev) * distanciaKnockback);
                easedPrev = eased;
            }

            if (visual != null && duracionEscala > 0f)
            {
                float u = Mathf.Clamp01(t / duracionEscala);
                visual.localScale = EscalaEnInstante(u, poseStretch, poseSquash, fuerzaOvershoot);
                AplicarInclinacion(AnguloEnInstante(u, anguloInclinacion, fuerzaOvershoot), worldTiltAxis);
            }
        }

        if (visual != null)
        {
            visual.localScale = escalaOriginal;
            visual.localRotation = rotacionOriginal;
        }
        rutinaActiva = null;
    }

    private Vector3 PoseStretch(float intensidad) => new Vector3(
        escalaOriginal.x * (1f - intensidad * 0.5f),
        escalaOriginal.y * (1f + intensidad),
        escalaOriginal.z * (1f - intensidad * 0.5f));

    private Vector3 EjeInclinacionMundo(Vector3 pushWorld)
    {
        Vector3 eje = Vector3.Cross(Vector3.up, pushWorld);
        return eje.sqrMagnitude > 0.0001f ? eje.normalized : Vector3.zero;
    }

    private void AplicarInclinacion(float angulo, Vector3 worldTiltAxis)
    {
        if (visual == null) return;
        if (worldTiltAxis != Vector3.zero && Mathf.Abs(angulo) > 0.0001f)
        {
            Vector3 localAxis = Quaternion.Inverse(transform.rotation) * worldTiltAxis;
            visual.localRotation = Quaternion.AngleAxis(angulo, localAxis) * rotacionOriginal;
        }
        else
        {
            visual.localRotation = rotacionOriginal;
        }
    }

    private Vector3 EscalaEnInstante(float u, Vector3 stretch, Vector3 squash, float overshoot)
    {
        if (u < FRACCION_SQUASH)
        {
            float k = u / FRACCION_SQUASH;
            k = 1f - Mathf.Pow(1f - k, 2f);
            return Vector3.LerpUnclamped(stretch, squash, k);
        }
        else
        {
            float k = (u - FRACCION_SQUASH) / (1f - FRACCION_SQUASH);
            float back = EaseOutBack(k, overshoot);
            return Vector3.LerpUnclamped(squash, escalaOriginal, back);
        }
    }


    private float AnguloEnInstante(float u, float anguloMax, float overshoot)
    {
        if (u < FRACCION_SQUASH)
        {
            float k = u / FRACCION_SQUASH;
            k = 1f - Mathf.Pow(1f - k, 2f);
            return Mathf.LerpUnclamped(0f, anguloMax, k);
        }
        else
        {
            float k = (u - FRACCION_SQUASH) / (1f - FRACCION_SQUASH);
            float back = EaseOutBack(k, overshoot);
            return Mathf.LerpUnclamped(anguloMax, 0f, back);
        }
    }

    private static float EaseOutBack(float x, float fuerza)
    {
        float c1 = 1.70158f * Mathf.Max(0f, fuerza);
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
