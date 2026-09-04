# Frostbound Frontier — prototipo Unity

Prototipo original de estrategia y supervivencia para Android horizontal, inspirado en los bucles generales del género sin reutilizar propiedad intelectual de Whiteout Survival.

## Abrir y jugar

1. En Unity Hub, elige **Add > Add project from disk**.
2. Selecciona esta carpeta y ábrela con Unity `6000.5.10f1`.
3. Espera la importación inicial. La escena `Assets/Scenes/FrostboundFrontier.unity` se crea automáticamente.
4. Pulsa **Play**.

## Regla visual del proyecto

Antes de crear cualquier icono, botón, panel, ventana o control nuevo, revisar primero `Assets/Skyden_Games/Free_Casual_GUI` y reutilizar un recurso compatible siempre que exista. `com.unity.ugui` se usa como sistema de componentes y eventos de UI; no se considera una biblioteca principal de arte. Si ningún recurso instalado encaja, documentar la necesidad antes de introducir una dirección visual diferente.

## Controles

- Ratón: arrastrar para mover; rueda para acercar/alejar.
- Móvil: arrastrar con un dedo; pellizcar con dos dedos.
- Selecciona un edificio en la barra inferior y usa **Mejorar**.

## Incluido

- Asentamiento 3D generado con primitivas, sin assets externos.
- Generador térmico, aserradero, cocina y refugio.
- Producción de calor, madera y comida.
- Seis trabajadores visibles en movimiento.
- Mejoras de edificios y costes progresivos.
- Guardado local y producción sin conexión limitada a dos horas.
- Interfaz adaptable a pantalla horizontal y área segura.
- Mejoras con cuenta regresiva, barra de progreso y una cola persistente.
- Temperatura global; el generador consume madera para conservar el calor.
- Salud y ánimo de la población afectados por frío y hambre.
- Seis supervivientes con asignación al aserradero y la cocina.
- Producción proporcional a trabajadores y nivel del edificio.
- Misión inicial para mejorar el generador, con recompensa automática.

## Probar el Hito 2

1. Selecciona **Generador térmico**, pulsa **Mejorar** y observa la barra de progreso.
2. Selecciona **Aserradero** o **Cocina comunal** y usa **Asignar/Quitar**.
3. Observa temperatura, madera, comida, salud y ánimo durante la simulación.
4. Al terminar el generador de nivel 2, la misión entrega 150 de madera y 100 de comida.

## Supabase

El prototipo está conectado al proyecto **War Tanks** de Supabase mediante un esquema relacional independiente con prefijo `frostbound_`.

- Crea una sesión anónima persistente por instalación.
- Guarda el estado general en `public.frostbound_players`.
- Sincroniza cada construcción por ranura en `public.frostbound_buildings`.
- Publica nivel de generador y poder en `public.frostbound_leaderboard`.
- Mantiene `public.frostbound_saves` únicamente como respaldo de emergencia, además del guardado local.
- Recupera el estado relacional remoto cuando es más reciente.
- Sincroniza automáticamente cada 15 segundos.
- Muestra el estado en la cabecera: `NUBE: RELACIONAL`, `NUBE: LOCAL` o `NUBE: ERROR`.
- Las tablas tienen RLS estricto; cada usuario solo puede modificar sus propias filas. El ranking es legible por jugadores autenticados.
- El cliente contiene únicamente una clave publicable. Nunca usa `service_role`.
- El esquema reproducible está en `Assets/Backend/frostbound_schema.sql`.

## Mapa mundial

- Mapa virtual de `1200 × 1200` sectores, centrado inicialmente en `(600, 600)`.
- El cliente mantiene en escena únicamente los sectores cercanos a la cámara y renueva el conjunto por chunks de 8 casillas.
- Cada cambio de chunk consulta a Supabase exclusivamente por el rectángulo visible mediante filtros `gte` / `lte` sobre `x` e `y`.
- El botón `MAPA MUNDIAL` alterna entre la colonia y la vista estratégica; `ASENTAMIENTO` regresa a la base.
- Al entrar desde el asentamiento, la cámara mundial se centra sobre la tile guardada de la ciudad y comienza con tamaño ortográfico `2.8`, que también es el acercamiento máximo.
- En el alejamiento máximo aparece un marcador verde sobre la ubicación exacta de la ciudad; seleccionar otra tile muestra una lupa naranja que inicia una aproximación cinematográfica al tocarla.
- Tocar una casilla la ilumina con un pulso cian y abre una tarjeta central con coordenadas, terreno, ocupante, nivel y las acciones contextuales `OCUPAR`, `REUBICAR` o `ATACAR`.
- `REUBICAR` activa una vista previa móvil de la base: toca otra tile para cambiar el destino, cancela sin guardar o confirma dos veces para efectuar el traslado.
- Las tiles ocupadas bloquean la confirmación; los destinos válidos se guardan localmente y se sincronizan mediante `frostbound_relocate_city` cuando hay sesión de Supabase.
- La tabla y políticas reproducibles están en `Assets/Backend/frostbound_world_map.sql`.

## Hito 3 — Marchas y recolección

- `frostbound_world_tiles` incluye tipo, capacidad y saldo restante para nodos de madera, comida y carbón.
- Supabase contiene 182 nodos persistentes: 60 de madera, 62 de comida y 60 de carbón.
- `frostbound_marches` guarda propietario, origen, destino, recurso, carga, tiempos y estado con RLS por usuario.
- `RECOLECTAR` inicia una marcha visual desde la ciudad; la duración se calcula usando la distancia entre coordenadas.
- La marcha progresa por `Marching`, `Gathering`, `Return` y `Completed`, persiste localmente y sincroniza cada transición con Supabase cuando hay sesión.
- Al regresar, entrega 250 unidades o el saldo disponible del nodo y el guardado relacional normal actualiza `frostbound_players`.
- La migración reproducible está en `Assets/Backend/frostbound_hito3_marches.sql`.

## Hito 4 — Cuartel, tropas y entrega atómica

- El Cuartel entrena lotes de 10 unidades de Infantería de Nieve en 10 segundos por 50 de comida.
- La cantidad entrenada se guarda en `frostbound_players.snow_infantry` y se recupera con el estado relacional.
- La tarjeta de cada nodo permite elegir tropas; cada infante aporta 50 unidades de capacidad de carga.
- `frostbound_complete_gather_march` bloquea marcha y nodo, descuenta `res_remaining`, acredita al jugador y completa la marcha en una sola transacción.
- Un nodo agotado cambia a `Empty` y limpia sus campos de recurso.
- La migración reproducible está en `Assets/Backend/frostbound_hito4_troops.sql`.

## Hito 5 — Bestias de Nieve y combate PVE

- El mapa contiene Lobos de Niebla y Osos Polares Glaciales con nivel, vida, poder recomendado y botín.
- La tarjeta PVE permite elegir Infantería de Nieve y muestra el poder total del escuadrón antes de atacar.
- Cada infante aporta 20 de poder; la RPC decide victoria, bajas, heridos y recompensa de forma atómica.
- Al vencer, la bestia desaparece y su tile vuelve a `Empty`; el botín y las bajas quedan persistidos en Supabase.
- La marcha retorna a la ciudad y abre un Informe de Batalla con resultado, bajas, heridos y botín.
- La migración reproducible está en `Assets/Backend/frostbound_hito5_beasts.sql`.

## Hito 6 — Enfermería y héroes

- La Enfermería muestra heridos, consume 2 de comida por tropa y ejecuta una curación temporizada.
- `frostbound_hospital` y sus RPC persisten el ingreso y la recuperación de tropas de forma atómica.
- `frostbound_heroes` entrega por defecto a Elena, Cazadora del Hielo, a cada jugador.
- Elena puede liderar recolección o PVE: aporta +15% de poder y +20% de velocidad de marcha.
- El panel de colección muestra retrato, nivel, estrellas, estadísticas y bonificaciones.
- La migración reproducible está en `Assets/Backend/frostbound_hito6_hospital_heroes.sql`.
