# ARQUITECTURA — TFG "Pulido Sensorial"

Banco de pruebas de *game feel* (inspirado en *Juice it or lose it*) en **Unity 2022.3 (URP)**.
La idea es tener un combate mínimo (jugador con espada vs. enemigos cápsula) y un conjunto de
**capas de pulido** que se pueden encender/apagar en vivo para medir cómo afecta cada una a la
percepción. El "cerebro" es `PolishManager`: cada efecto consulta su bool antes de ejecutarse.

> Este documento describe el **estado actual** del proyecto leído de los scripts y de la escena
> `Assets/Scenes/Demo.unity` (a 2026-07-10).
>
> **Limpieza de código (2026-07-09):** los scripts de `Assets/Scripts/` se pasaron por una revisión de
> legibilidad y consistencia, **sin cambiar el comportamiento** de la demo. Convención: identificadores en
> español (PascalCase para tipos/métodos, camelCase para campos/locales), comentarios en español que
> empiezan en minúscula y explican el *porqué*, y formato uniforme. Se quitó código muerto (usings sin usar,
> `Debug.Log` de depuración, campos que no se leían). Fue **conservador**: NO se renombraron campos
> serializados ni clases (para no romper las referencias de la escena), así que `enemyManager` conserva su
> nombre en minúscula.
>
> **Poda de experimentos abandonados (2026-07-10):** se eliminaron del proyecto (código, componentes de
> escena y assets) varios experimentos descartados: `EspadaStretch` (smear de la espada) y su hook
> `SwordAttack.SwingProgress01`; el suelo de agua (`SueloCapa6`, `AguaCapa6.shadergraph`, `Mat_AguaCapa6.mat`
> y el pack externo `WaterRippleShader`); `ImpactEffectsManager` (código muerto); `CameraFollow` (la cámara la
> controla Cinemachine); y el sistema de **presets con flechas ←/→** de `PolishManager`. Quedan **9 scripts**.

---

## 1. Las 6 capas de pulido (bools de `PolishManager`)

`PolishManager` es un singleton (`PolishManager.Instance`) con 6 bools públicos. Control por teclado:
**teclas 1–6** togglean cada capa y **tecla T** alterna todas a la vez (todo ON / todo OFF); la **tecla E**
(reinicia la oleada) la gestiona `enemyManager`. El estado de cada capa se muestra con una **fila de
`Image`s** (`toggleUIs`, HUD siempre visible: alfa 0.9 = ON, 0.4 = OFF).

| Tecla | Bool (`PolishManager`) | Capa / qué representa            | ¿Conectado hoy? |
|:-----:|------------------------|----------------------------------|-----------------|
| 1     | `enemigoReacciona`     | El enemigo reacciona al golpe    | **Sí** — gate de `Enemy.OnHitPolish` (flash + reacción cinética + partículas) y de `EnemyReaction` |
| 2     | `espadaExpresiva`      | La espada se expresa al atacar   | **Sí** — gate del **trail** de la espada (`SwordAttack.ToggleTrails`) |
| 3     | `camaraResponde`       | Cámara y pantalla responden      | **Sí** — gate de `Enemy.DispararRespuestaCamara` (camera shake + hitstop + impact frame) |
| 4     | `sonido`               | Sonido de impacto                | **Sí** — gate de los SFX: `Enemy` (sfxToque/sfxMuerte) + `SwordAttack` (sfxWhoosh), solo `.Play()` |
| 5     | `interfaz`             | UI de feedback                   | **Sí** — gate de los **controles en pantalla** (`ControlUIManager`) y de la **barra de vida** de cada enemigo (`Enemy.lifebarCanvas`) |
| 6     | `mundo`                | Capa atmosférica                 | **Sí** — gate del bloque de mundo en `PolishManager.Update` (materiales de cápsulas + 2 luces + post-pro + partículas emisivas) |

**Conclusión:** las **6 capas** están cableadas. La capa 6 actúa sobre las cápsulas, dos luces, el
`globalVolume` y las partículas emisivas; el antiguo intercambio de material del suelo (agua) se eliminó.

---

## 2. Scripts existentes (`Assets/Scripts/`)

### `PolishManager.cs`
Singleton experimental. Define las 6 capas (bools) y el control por teclado: **1–6** togglean cada capa y
**T** alterna todas a la vez (`ActivarTodas(true/false)`). En `Awake` arranca con todas apagadas. Dibuja el
HUD de estado (`toggleUIs`, lista de `Image`s coloreadas por capa: alfa 0.9 ON / 0.4 OFF). **Además ejecuta
la capa 6 (`mundo`)** en `Update`:
- Con `mundo` ON: activa `globalVolume` (post-pro), enciende `polishedLight` y apaga `defaultLight`
  (**dos fuentes de luz**), activa `firefliesParticleSystem` (**partículas emisivas**) y sube el
  `_Metallic`/`_Smoothness` de `capsuleMaterials` (**cambio de materiales**).
- Con `mundo` OFF: revierte todo (materiales a metallic 0 / smoothness 0.5, `defaultLight` ON y el
  resto OFF).
- Referencias de la capa 6 asignadas en el Inspector: `globalVolume`, `defaultLight`, `polishedLight`,
  `capsuleMaterials[]`, `firefliesParticleSystem`.

Salvo el bloque de `mundo`, sigue siendo el estado que consulta el resto de scripts.

### `Enemy.cs`  *(en la raíz del prefab enemigo)*
Persigue al Player sobre el plano (con *stopping distance* aleatorizada y separación entre enemigos),
rota para mirarlo, y gestiona la vida y los golpes. Núcleo de golpes:
- `TakeHit(attackerPosition)` — resta vida, calcula `esCritico = (vidaActual <= 0)`, actualiza la
  **barra de vida** (`HandleLifebar`), reproduce el SFX (capa 4), llama a `OnHitPolish(...)` y a
  `DispararRespuestaCamara(...)` (capa 3). Si es crítico y la reacción **no** gestiona la desaparición,
  hace la muerte seca vía `DesaparecerAlMorir()`.
- `OnHitPolish(attackerPosition, esCritico) → bool` — **gated por `enemigoReacciona`** (capa 1).
  Dispara: (1) **hit flash**, (2) **reacción cinética** vía `EnemyReaction.Reaccionar`, (3) **partículas**
  de impacto. Devuelve si la reacción se encarga de la muerte.
- `CorrutinaFlash(esCritico)` — **intercambia el material** del renderer por instancia
  (`rend.material = flashMat`), espera y lo restaura (`rend.material = defaultMat`). Solo la **duración**
  cambia toque/crítico (`duracionFlashToque` / `duracionFlashCritico`).
- `DispararParticulas(esCritico)` — instancia `particulasToque`/`particulasCritico` en el centro del
  cuerpo visible; toque → parentado a la raíz; crítico → suelto en el mundo; reproduce burst + hijos
  (`ReproducirConHijos`) y se autodestruye (`DuracionTotalSistema` + `margenDestruccion`).
- `DispararRespuestaCamara(esCritico)` — **bloque de la capa 3 (`camaraResponde`)**, disparado desde
  `TakeHit` (independiente de la capa 1) y **antes** de un posible `SetActive(false)`. TOQUE → solo
  camera shake; CRÍTICO → hitstop (+ impact frame) + camera shake. Con `camaraResponde` OFF, nada.
- `CameraShake / CameraShakeCoroutine` — sacude una `CinemachineVirtualCamera` (encontrada con
  `FindObjectOfType` en `Start`) vía `CinemachineBasicMultiChannelPerlin`. La corrutina se **hospeda en
  la cámara** (`cam.StartCoroutine`) para que el shake termine y resetee la noise aunque el enemigo se
  desactive al morir. Valores de shake/hitstop **expuestos en el inspector** (toque/crítico).
- `ReproducirSonido(AudioSource)` — **bloque de la capa 4 (`sonido`)**, disparado desde `TakeHit` con la
  distinción toque/crítico (`sfxToque` en golpe normal, `sfxMuerte` en crítico/muerte). Solo llama a
  `.Play()` sobre un `AudioSource` con su clip ya asignado en el Inspector (sin `PlayOneShot`,
  `AddComponent` ni tocar el pitch). Solo suena si `sonido` está ON; avisa por consola si el
  `AudioSource` o su clip están sin asignar.
- `HandleLifebar(vida)` — **capa 5**: recalcula el `offsetMax` del relleno del `lifebarCanvas`
  (world canvas) para dibujar la vida restante. En `Update`, `lifebarCanvas` se muestra/oculta según
  `interfaz`.
- `DesaparecerAlMorir()` / `DesaparecerCuandoTermineSonido()` — **desaparición de la muerte centralizada**.
  Como el `AudioSource` de `sfxMuerte` vive en el enemigo, un `SetActive(false)` inmediato cortaría el
  clip. La corrutina pone `muerto = true` (congela `Update`: deja de perseguir) y **espera a que
  `sfxMuerte` termine** (`while (isPlaying)`) antes de `SetActive(false)`. Si `sonido` está OFF / sin
  clip → desaparición inmediata. La llaman la **muerte seca** (capa 1 OFF, desde `TakeHit`) y
  **`EnemyReaction`** al acabar la animación de muerte (capa 1 ON). `OnEnable` resetea `muerto`.

### `EnemyReaction.cs`  *(en la raíz del prefab enemigo, junto a `Enemy`)*
Parte **cinética** de la capa 1. **Gated por `enemigoReacciona`**. Deforma/mueve el hijo `Visual`
(nunca la raíz, que necesita su rotación de mirada). `Reaccionar(attackerPosition, esCritico) → bool`
(true si gestiona la desaparición):
- **Golpe normal** (`RutinaReaccion`): knockback de posición (ease-out incremental, coordinado con la
  persecución vía `yield return null`), **squash & stretch** (stretch vertical → squash → vuelta con
  *overshoot* ease-out-back) e **inclinación tipo bolo** hacia el empuje que se reendereza con rebote.
  Restaura escala y rotación originales al terminar.
- **Golpe crítico / muerte** (`RutinaMuerte`): reusa la **inclinación** (cae hacia el empuje hasta
  `anguloMuerte` y **NO se reendereza**), con knockback más lejano/lento. **No deforma la escala** y no
  restaura nada (muere caído). Al terminar delega en `Enemy.DesaparecerAlMorir()` (que espera al SFX de
  muerte de la capa 4 antes de `SetActive(false)`); fallback a `SetActive(false)` si no hubiera `Enemy`.
- Helpers compartidos: `PoseStretch`, `EjeInclinacionMundo`, `AplicarInclinacion`. Bandera `muriendo`
  (ignora golpes nuevos y corta la reacción normal en curso). `OnEnable` restaura pose original (para
  reciclaje). Lee escala/rotación originales del `Visual` en `Awake`.

### `SwordAttack.cs`  *(en el Player)*
Ataque por corrutina. Jerarquía esperada: `Player → PivoteEspada (vacío) → Espada`. Con `Fire1`/`Espacio`
hace el swing (rota el `PivoteEspada` en arco con `SmoothStep`, ida y vuelta), aplica **cooldown**, y
detecta impactos en cono frontal (`OverlapSphere` + ángulo) llamando a `Enemy.TakeHit(transform.position)`
(un golpe por enemigo y swing). **Trail = capa 2 (`espadaExpresiva`):**
- `InicializarTrails()` en `Start` deja los `TrailRenderer` de `trailParent` activos pero con
  `emitting = false` y `Clear()` → arrancan limpios, sin estela siguiendo al jugador.
- `ToggleTrails(bool)` controla la **emisión** (no `enabled`) al inicio/fin de cada swing, y **solo emite
  si `PolishManager.Instance.espadaExpresiva` está ON**; al activar hace `Clear()` para no unir con la
  estela anterior. Con la capa OFF, el trail no emite en ningún momento.
- **Sonido (capa 4):** `sfxWhoosh` (AudioSource asignado en el Inspector) se reproduce **al iniciar el
  swing** (no al impactar), vía `ReproducirSonido()` — solo si `PolishManager.Instance.sonido` está ON.

### `PlayerMovement.cs`  *(en el Player)*
Movimiento WASD/flechas relativo a la cámara, con rotación suave hacia la dirección (forzando arco
corto). `OnTriggerEnter` con tag `HitCollider` solo hace un `Debug.Log` ("Me han pegao :(") — el jugador
no tiene vida ni daño (es "inmortal").

### `ControlUIManager.cs`  *(capa 5 — UI, en el canvas de controles)*
Muestra los **controles en pantalla** (imágenes W / A / S / D / Espacio / clic). En `Update`, **gated por
`interfaz`**: si la capa está ON activa las imágenes, si está OFF las oculta. Además ilumina cada tecla
(sube el alfa) mientras se pulsa (`GetKeyDown`/`GetKeyUp`, y clic con `GetMouseButton`).

### `BillboardCanvas.cs`  *(en los world canvas)*
En `LateUpdate` orienta el canvas para que mire siempre a la cámara (`transform.forward =
camara.transform.forward`, con `camara = Camera.main`). Lo usan las UI en el mundo (p. ej. la barra de
vida del enemigo).

### `HitstopManager.cs`  *(singleton, en el objeto `PolishManager` de la escena)*
`Instance`. `CallHitstop(delay, frames)` → corrutina que: activa un `colorCanvas` (overlay de color a
pantalla completa = **"impact frame"** / fogonazo), espera `delay`, pone `Time.timeScale = 0` durante
`frames` *end-of-frame*, restaura el `timeScale` y oculta el `colorCanvas`. **Hitstop e impact-frame
están acoplados** en este script. Lo llama la capa 3 (`Enemy.DispararRespuestaCamara`, solo en crítico).

### `enemyManager.cs`  *(spawner de pruebas)*
Helper de test: con la tecla **E** destruye la oleada actual (si la hay) e instancia una nueva como hija
de este objeto, para repetir el test de golpes sin reiniciar la escena. *(La clase conserva el nombre en
minúscula `enemyManager`; renombrarla tocaría la escena, así que se dejó tal cual en la limpieza.)*

> **Cámara:** la Main Camera la controla **Cinemachine** (una `CinemachineVirtualCamera` + `CinemachineBrain`);
> el antiguo `CameraFollow` se eliminó. `PlayerMovement` y `BillboardCanvas` usan `Camera.main` (esa Main Camera).

> Otros scripts del proyecto (`Assets/TutorialInfo/...`, `Readme*`) son de la plantilla de Unity, no del TFG.

---

## 3. Convención TOQUE vs CRÍTICO

El **mismo golpe** se resuelve distinto según si mata o no. La fuente de verdad es `Enemy.TakeHit`:

```
vidaActual--;
bool esCritico = (vidaActual <= 0);   // este golpe lleva la vida a 0
```

Ese `esCritico` se propaga a todos los efectos por parámetro. Efectos que usan la convención:

| Efecto | TOQUE (`esCritico = false`) | CRÍTICO (`esCritico = true`) |
|--------|------------------------------|------------------------------|
| **Hit flash** (`Enemy.CorrutinaFlash`) | swap a `flashMat` durante `duracionFlashToque` (sutil) | swap a `flashMat` durante `duracionFlashCritico` (más largo). *Mismo material; solo cambia la duración.* |
| **Reacción cinética** (`EnemyReaction`) | `RutinaReaccion`: squash & stretch + inclinación con rebote; el enemigo sobrevive | `RutinaMuerte`: cae y no se reendereza, knockback más lejano, y desaparece |
| **Partículas** (`Enemy.DispararParticulas`) | prefab `particulasToque`, parentado a la raíz | prefab `particulasCritico` (con onda hija), suelto en el mundo |
| **Camera shake** (capa 3, `Enemy.DispararRespuestaCamara`) | shake con parámetros *Toque* (def. 10 / 6 / 0.05) | shake con parámetros *Crítico* (def. 18 / 10 / 0.20) |
| **Hitstop + impact frame** (capa 3, `HitstopManager`) | — (no se dispara) | `CallHitstop(hitstopDelay, hitstopFrames)` (def. 0.05 / 40) |
| **Sonido** (capa 4, `Enemy.ReproducirSonido`) | `sfxToque.Play()` | `sfxMuerte.Play()` |

> El **whoosh** de la espada (capa 4, `SwordAttack.sfxWhoosh`) no usa la distinción toque/crítico: suena
> **al iniciar cada swing**, antes de saber si impacta.

> Camera shake e hitstop están en la **capa 3 (`camaraResponde`)**, independientes de la capa 1
> (se disparan desde `Enemy.TakeHit` vía `DispararRespuestaCamara`).

**Patrón general:** cada efecto recibe `bool esCritico` y elige un juego de parámetros u otro. Los
valores de reacción, muerte, partículas, camera shake e hitstop están **expuestos en el inspector**
(filosofía del proyecto: sin números mágicos).

---

## 4. Estructura del prefab del enemigo

```
Enemy (raíz)                ← objeto que se mueve por el mundo
 ├─ Script Enemy            (persecución, vida, flash, partículas [capa 1] + shake/hitstop [capa 3] + sonido [capa 4] + barra de vida [capa 5])
 ├─ Script EnemyReaction    (knockback + squash&stretch + inclinación + muerte; deforma al Visual)
 ├─ CapsuleCollider         (detección de golpes)
 ├─ Layer de enemigos       (para OverlapSphere de separación y de detección de la espada)
 ├─ AudioSource(s)          (sfxToque / sfxMuerte, capa 4)
 ├─ lifebarCanvas (hijo)    ← barra de vida (capa 5); world canvas con BillboardCanvas
 └─ Visual (hijo)           ← LO QUE SE DEFORMA (squash/stretch/inclinación)
      ├─ MeshRenderer        (cápsula; `Enemy.rend` se obtiene con GetComponentInChildren)
      ├─ Material emisivo    (para el hit flash: se intercambia por `flashMat` y vuelta a `defaultMat`)
      └─ Ojos (hijos del Visual)
```

Claves:
- La **raíz** se mueve/rota (mira al Player); el **Visual** es lo único que se escala/inclina, para no
  ensuciar la rotación de mirada.
- El **flash** intercambia `rend.material` (copia por instancia) → no afecta a otros enemigos.
- Campos a asignar en el **prefab** (se propagan a todas las instancias): en `EnemyReaction`, la
  referencia al `Visual`; en `Enemy`, los prefabs `particulasToque`/`particulasCritico`, los `AudioSource`
  y el `lifebarCanvas`.
- En la **muerte** el crítico acaba en `SetActive(false)` de la raíz: por eso las partículas del crítico
  se instancian **sueltas**, y el **SFX de muerte** (capa 4) no se corta (la desactivación se retrasa
  hasta que el clip acaba, `Enemy.DesaparecerAlMorir`).

---

## 5. NOTAS Y PENDIENTES

### Eliminado (poda 2026-07-10)
- **Capa 2 — `EspadaStretch`** (smear de la hoja): descartado y **eliminado** (script, componente y el hook
  `SwordAttack.SwingProgress01`). La capa 2 se queda solo con el trail.
- **Capa 6 — suelo de agua**: **eliminado** por completo (`SueloCapa6`, `AguaCapa6.shadergraph`,
  `Mat_AguaCapa6.mat` y el pack externo `WaterRippleShader`). El suelo usa su material básico fijo; la capa 6
  ya no toca el material del suelo.
- **`ImpactEffectsManager`** (pulso Bloom/Vignette): era código muerto, **eliminado**. El post-pro base lo
  activa la capa 6 vía `globalVolume`.
- **`CameraFollow`**: **eliminado**; la cámara la controla solo Cinemachine.
- **Presets con flechas ←/→** de `PolishManager`: **eliminados**. Quedan toggles 1–6 y T.

### Capa 4 — variación de tono y SFX de daño al Player
- La **variación de tono** de los SFX, si se usa, está **fuera de los scripts** (solo `.Play()`, sin tocar el
  pitch): se configuraría a nivel de `AudioSource` / asset de audio.
- El **SFX de "sufrir daño el Player"** quedó pendiente/descartado (`PlayerMovement.OnTriggerEnter` solo hace
  un `Debug.Log("Me han pegao :(")`; el jugador no tiene vida ni daño).

---

## Resumen de un vistazo

- **Capa 1 (`enemigoReacciona`)** = todo lo que le pasa al enemigo al ser golpeado: flash (swap
  `flashMat`↔`defaultMat`) + reacción cinética (squash/stretch/inclinación/muerte) + partículas. **Cableada.**
- **Capa 2 (`espadaExpresiva`)** = **solo el trail** de la espada (arranque limpio `emitting=false`/`Clear()`,
  emite solo durante el swing con la capa ON). **Cableada.**
- **Capa 3 (`camaraResponde`)** = hitstop + impact frame + camera shake (bloque en
  `Enemy.DispararRespuestaCamara`, disparado desde `TakeHit`, independiente de la capa 1). **Cableada.**
- **Capa 4 (`sonido`)** = SFX de espada (whoosh al atacar, `SwordAttack`) + toque y muerte del enemigo
  (`sfxToque`/`sfxMuerte`, `Enemy`), gating por `sonido`, solo `.Play()`. **Cableada.** (Variación de tono
  a nivel de asset; SFX de daño al Player pendiente/descartado.)
- **Capa 5 (`interfaz`)** = controles en pantalla (`ControlUIManager`) + barra de vida de los enemigos
  (`Enemy.lifebarCanvas` + `BillboardCanvas`), gating por `interfaz`. **Cableada.**
- **Capa 6 (`mundo`)** = cambio de materiales de las cápsulas (metallic/smoothness) + dos fuentes de luz
  (`polishedLight`/`defaultLight`) + post-procesado (`globalVolume`) + partículas emisivas
  (`firefliesParticleSystem`), todo en `PolishManager.Update`. **Cableada.**
- **Convención toque/crítico** = `esCritico` nace en `Enemy.TakeHit` y se propaga a cada efecto.
