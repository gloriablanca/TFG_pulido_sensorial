using UnityEngine;
using System.Collections;
using Cinemachine;
using UnityEngine.UI;

public class Enemy : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField, Range(0.5f, 10f)] private float velocidad = 3f;

    [Header("Stopping distance")]
    [SerializeField, Range(0.1f, 5f)] private float stoppingDistance = 1.2f;
    [SerializeField, Range(0f, 1f)] private float margenStoppingDistance = 0.5f;

    [Header("Separación enemigos")]
    [SerializeField, Range(0.5f, 5f)] private float radioSeparacion = 1.5f;
    [SerializeField, Range(0f, 2f)] private float pesoSeparacion  = 0.5f;
    [SerializeField] private LayerMask capaEnemigos;

    [Header("Vida")]
    [SerializeField, Range(1, 20)] private int vidaMaxima = 3;

    //flash capa1
    [Header("Flash (intercambio material)")]
    [Tooltip("Material normal")]
    [SerializeField] private Material defaultMat;
    [Tooltip("Material flash")]
    [SerializeField] private Material flashMat;
    [Tooltip("Duración flash normal en segs")]
    [SerializeField, Range(0.01f, 0.5f)] private float duracionFlashToque   = 0.08f;
    [Tooltip("Duración flash mortal en segs")]
    [SerializeField, Range(0.01f, 1f)]   private float duracionFlashCritico = 0.25f;

    //partículas capa 1
    [Header("Partículas golpe")]
    [Tooltip("Prefab ChispasToque golpe normal")]
    [SerializeField] private ParticleSystem particulasToque;
    [Tooltip("Prefab ChispasFinal golpe mortal")]
    [SerializeField] private ParticleSystem particulasCritico;
    [Tooltip("Tiempo antes de destruir la instancia sobre lo que dura")]
    [SerializeField, Range(0f, 2f)] private float margenDestruccion = 0.5f;

    //capa 3 camera shake + hitstop (+ impact frame)
    [Header("Camera shake normal")]
    [Tooltip("Amplitud")]
    [SerializeField, Range(0f, 30f)] private float shakeAmplitudToque = 10f;
    [Tooltip("Frecuencia")]
    [SerializeField, Range(0f, 30f)] private float shakeFrecuenciaToque = 6f;
    [Tooltip("Duración")]
    [SerializeField, Range(0f, 1f)] private float shakeDuracionToque = 0.05f;

    [Header("Camera shake mortal")]
    [Tooltip("Amplitud")]
    [SerializeField, Range(0f, 30f)] private float shakeAmplitudCritico = 18f;
    [Tooltip("Frecuencia")]
    [SerializeField, Range(0f, 30f)] private float shakeFrecuenciaCritico = 10f;
    [Tooltip("Duración")]
    [SerializeField, Range(0f, 1f)] private float shakeDuracionCritico = 0.20f;

    [Header("Hitstop + impact frame (mortal)")]
    [Tooltip("Retardo antes de congelar el tiempo")]
    [SerializeField, Range(0f, 0.5f)] private float hitstopDelay = 0.05f;
    [Tooltip("Cuántos frames dura")]
    [SerializeField, Range(0, 120)] private int hitstopFrames = 40;


    [Header("Sonido")]
    [Tooltip("golpe normal")]
    [SerializeField] private AudioSource sfxToque;
    [Tooltip("golpe mortal")]
    [SerializeField] private AudioSource sfxMuerte;

    [Header("UI")]
    [Tooltip("Barra de vida")]
    [SerializeField] private Canvas lifebarCanvas;

    //cámara virtual de Cinemachine (para el shake de la capa 3)
    private CinemachineVirtualCamera cam;

    private int       vidaActual;
    private Transform jugador;
    private bool      muerto;
    private Renderer  rend;
    private Coroutine flashActivo;

    void Start()
    {
        cam = FindObjectOfType<CinemachineVirtualCamera>();
        vidaActual = vidaMaxima;

        //randomizo un poco la stopping distance para que los enemigos no se apelotonen igual
        stoppingDistance += Random.Range(-margenStoppingDistance, margenStoppingDistance);
        stoppingDistance  = Mathf.Max(0.1f, stoppingDistance);

        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go != null)
            jugador = go.transform;
        else
            Debug.LogWarning("[Enemy] No hay GameObject con tag 'Player'.", this);

        rend = GetComponentInChildren<Renderer>();
    }

    //si el enemigo se reactiva tras morir vuelve a estar "vivo"
    void OnEnable()
    {
        muerto = false;
    }

    void Update()
    {
        lifebarCanvas.gameObject.SetActive(PolishManager.Instance.interfaz);

        if (muerto) return;
        if (jugador == null) return;

        Vector3 dir = jugador.position - transform.position;
        dir.y = 0f;
        float distancia = dir.magnitude;

        Vector3 dirPersecucion = (distancia > stoppingDistance) ? dir.normalized : Vector3.zero;
        Vector3 separacion     = CalcularSeparacion();

        Vector3 movimiento = dirPersecucion + separacion * pesoSeparacion;
        movimiento.y = 0f;

        if (movimiento.sqrMagnitude > 0.001f)
            transform.position += movimiento.normalized * velocidad * Time.deltaTime;

        if (distancia > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    //para separarse de los enemigos vecinos (evita que se apilen en el mismo punto)
    Vector3 CalcularSeparacion()
    {
        Vector3 sep = Vector3.zero;

        Collider[] vecinos = Physics.OverlapSphere(
            transform.position, radioSeparacion, capaEnemigos);

        foreach (Collider c in vecinos)
        {
            if (c.gameObject == gameObject) continue;
            if (!c.TryGetComponent<Enemy>(out _)) continue;

            Vector3 alejarse = transform.position - c.transform.position;
            alejarse.y = 0f;
            float dist = alejarse.magnitude;
            if (dist < 0.001f) continue;

            float fuerza = Mathf.Max(0f, 1f - dist / radioSeparacion); //más cerca = más empuje
            sep += alejarse.normalized * fuerza;
        }

        return Vector3.ClampMagnitude(sep, 1f);
    }


    public void TakeHit(Vector3 attackerPosition)
    {
        if (muerto) return;

        vidaActual--;
        bool esCritico = (vidaActual <= 0);

        HandleLifebar(vidaActual);

        ReproducirSonido(esCritico ? sfxMuerte : sfxToque);

        bool muerteGestionada = OnHitPolish(attackerPosition, esCritico);

        DispararRespuestaCamara(esCritico);

        if (esCritico && !muerteGestionada)
            DesaparecerAlMorir();
    }

    //recalcula el ancho del relleno de la barra de vida (offsetMax) según la vida qie quede
    void HandleLifebar(int vida)
    {
        float segmentSize = 1 / (float)vidaMaxima;
        float rightMargin = segmentSize * (float)(vidaMaxima - vida);
        Image barFill = lifebarCanvas.GetComponentsInChildren<Image>()[1];
        barFill.rectTransform.offsetMax = new Vector2(-rightMargin, 0f);
    }

    public void DesaparecerAlMorir()
    {
        StartCoroutine(DesaparecerCuandoTermineSonido());
    }

    IEnumerator DesaparecerCuandoTermineSonido()
    {
        muerto = true;

        if (sfxMuerte != null)
            while (sfxMuerte.isPlaying) yield return null;

        gameObject.SetActive(false);
    }

    void ReproducirSonido(AudioSource source)
    {
        if (PolishManager.Instance == null || !PolishManager.Instance.sonido) return;

        if (source == null)
        {
            Debug.LogWarning("[Enemy] SFX sin asignar (AudioSource null). Arrástralo en el Inspector.", this);
            return;
        }
        if (source.clip == null)
        {
            Debug.LogWarning($"[Enemy] El AudioSource '{source.name}' no tiene Clip asignado.", source);
            return;
        }

        source.Play();
    }

    public bool OnHitPolish(Vector3 attackerPosition, bool esCritico)
    {
        //flash + reacción solo si la capa 1 está ON (y existe PolishManager)
        if (PolishManager.Instance == null || !PolishManager.Instance.enemigoReacciona) return false;

        //flash solo en ESTE enemigo (rend.material crea una copia propia del material)
        if (rend != null)
        {
            if (flashActivo != null) StopCoroutine(flashActivo);
            flashActivo = StartCoroutine(CorrutinaFlash(esCritico));
        }

        var reaction = GetComponent<EnemyReaction>();
        bool muerteGestionada = (reaction != null) && reaction.Reaccionar(attackerPosition, esCritico);

        DispararParticulas(esCritico);

        return muerteGestionada;
    }

    //flash por intercambio de material, 'esCritico' solo cambia la duración
    IEnumerator CorrutinaFlash(bool esCritico)
    {
        float duracion = esCritico ? duracionFlashCritico : duracionFlashToque;

        rend.material = flashMat;
        yield return new WaitForSeconds(duracion);
        rend.material = defaultMat;

        flashActivo = null;
    }


    void DispararParticulas(bool esCritico)
    {
        ParticleSystem prefab = esCritico ? particulasCritico : particulasToque;
        if (prefab == null) return;

        Vector3 puntoImpacto = (rend != null) ? rend.bounds.center : transform.position;

        ParticleSystem inst = Instantiate(prefab, puntoImpacto, Quaternion.identity);

        if (!esCritico)
            inst.transform.SetParent(transform, worldPositionStays: true);

        ReproducirConHijos(inst);

        float vida = DuracionTotalSistema(inst) + margenDestruccion;
        Destroy(inst.gameObject, vida);
    }


    static void ReproducirConHijos(ParticleSystem raiz)
    {
        foreach (ParticleSystem ps in raiz.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Clear(false);
            ps.Play(false);
        }
    }

    static float DuracionTotalSistema(ParticleSystem ps)
    {
        float total = 0f;
        foreach (ParticleSystem sub in ps.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = sub.main;
            float fin = main.duration + main.startLifetime.constantMax;   // emisión + vida máxima de partícula
            if (fin > total) total = fin;
        }
        return total;
    }

    void DispararRespuestaCamara(bool esCritico)
    {
        if (PolishManager.Instance == null || !PolishManager.Instance.camaraResponde) return;

        if (esCritico)
        {
            //hitstop + impact frame (el colorCanvas lo gestiona HitstopManager) -> si no existe, se omite
            if (HitstopManager.Instance != null)
                HitstopManager.Instance.CallHitstop(hitstopDelay, hitstopFrames);
            CameraShake(shakeAmplitudCritico, shakeFrecuenciaCritico, shakeDuracionCritico);
        }
        else
        {
            CameraShake(shakeAmplitudToque, shakeFrecuenciaToque, shakeDuracionToque);
        }
    }

    void CameraShake(float amplitud, float frecuencia, float duracion)
    {
        if (cam == null) return;
        //corrutina en la cámara NO en el enemigo: así el shake se completa y resetea la noise aunque el enemigo se desactive al morir
        //porque si corriera en el enemigo se cortaría
        cam.StartCoroutine(CameraShakeCoroutine(amplitud, frecuencia, duracion));
    }

    IEnumerator CameraShakeCoroutine(float amplitud, float frecuencia, float duracion)
    {
        var noise = cam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
        if (noise == null) yield break;

        noise.m_AmplitudeGain = amplitud;
        noise.m_FrequencyGain = frecuencia;

        yield return new WaitForSeconds(duracion);

        noise.m_AmplitudeGain = 0f;
        noise.m_FrequencyGain = 0f;
    }
}
