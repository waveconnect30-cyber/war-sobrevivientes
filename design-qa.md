# QA visual — selección de casilla mundial

## Referencia

Estado comparado: una casilla del mapa mundial seleccionada, con tarjeta informativa abierta.

## Comprobaciones

- [x] La casilla seleccionada queda claramente marcada con borde, núcleo emisivo y pulso cian.
- [x] La tarjeta aparece centrada sin ocultar la navegación principal del mapa horizontal.
- [x] La jerarquía visual conserva la referencia: previsualización azul, coordenadas, título del terreno, información del ocupante y dos acciones grandes.
- [x] El contenido cambia según la casilla: terreno, nivel, ocupante y acción `OCUPAR` o `ATACAR`.
- [x] La tarjeta incluye cierre explícito y los clics dentro de ella no seleccionan accidentalmente otra casilla.
- [x] Al entrar al mapa, el clic usado para cambiar de vista no se propaga a una casilla.
- [x] El contraste es legible en el simulador horizontal y mantiene el lenguaje visual frío del prototipo.

## Adaptación

La ilustración fija de montaña de la referencia se sustituyó por una previsualización dinámica del tipo de terreno. Esto permite que una sola tarjeta represente hielo, recursos, bestias, fortalezas y ciudades sin mostrar información incorrecta.

## Resultado

Passed — interacción y composición verificadas en Unity Play Mode.

## QA del zoom mundial

- [x] El zoom máximo muestra aproximadamente cuatro filas completas de tiles; una mina de recursos continúa ocupando exactamente una casilla.
- [x] En formato horizontal se conservan cerca de once columnas visibles, equivalente a la densidad de la referencia adaptada desde su formato vertical.
- [x] La rueda del mouse y el gesto de pellizco respetan el mismo límite mínimo y máximo.
- [x] Se añadieron controles táctiles `−` y `+` en la barra inferior sin interferir con la selección de tiles.
- [x] Los límites evitan acercar la cámara hasta recortar una sola casilla o alejarla fuera del rango útil del mapa.

final result: passed

## QA de navegación global

- [x] En zoom-out máximo la ciudad conserva un marcador verde de tamaño legible, independiente del tamaño visual de las tiles.
- [x] Tocar una tile en esta escala crea un marcador naranja con lupa y coordenadas.
- [x] El marcador de búsqueda permanece anclado a la coordenada mundial seleccionada.
- [x] Un segundo toque inicia una transición suavizada de 1.65 segundos que desplaza y acerca la cámara hasta zoom `2.8`.
- [x] Durante la transición se bloquea el arrastre accidental y al terminar reaparece la selección normal de la tile.

final result: passed

## QA de reubicación

- [x] `REUBICAR` cambia de la tarjeta informativa a un modo de colocación dedicado.
- [x] La vista previa de la base se mueve al tocar otra tile, compatible con entrada táctil.
- [x] Las posiciones disponibles e inválidas utilizan estados visuales distintos.
- [x] `CANCELAR` abandona el modo sin modificar la posición guardada.
- [x] El destino requiere una segunda confirmación explícita antes del traslado.
- [x] Los controles de reubicación bloquean los clics para que no atraviesen hacia el mundo.

final result: passed
