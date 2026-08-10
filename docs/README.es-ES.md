# Private Playback — guía en castellano

Private Playback `0.9.0` es un complemento beta, independiente y no oficial para Jellyfin Server **exactamente 10.11.11**. Permite aplicar por ID de usuario una política que impide que el core conserve determinadas categorías de UserData de reproducción.

## Qué protege

El preset **Navegación privada completa** conserva el valor previo de:

- posición de reanudación y su efecto en «Continuar viendo»;
- estado visto/no visto, incluidos los cambios manuales por API;
- contador de reproducciones y fecha de última reproducción.

No borra datos antiguos al activarse. Favoritos, valoración y preferencias de pista de audio/subtítulos siguen funcionando. Tampoco altera permisos, contraseñas, dispositivos, autenticación ni logs de seguridad.

El botón «Visto» puede continuar visible: Jellyfin 10.11.11 no ofrece un punto oficial para retirarlo por usuario en todos los clientes. La garantía está en el servidor; después de la petición, el estado persistente continúa siendo el permitido por la política.

Otros complementos como Playback Reporting, Webhook o Trakt pueden registrar eventos por su cuenta. Private Playback no bloquea eventos ni toca bases de datos ajenas.

## Instalación

1. Comprueba `private-playback_0.9.0.0.zip` con `SHA256SUMS`.
2. Detén Jellyfin.
3. Crea `plugins/Private Playback_0.9.0.0` bajo el directorio de datos de Jellyfin.
4. Extrae el ZIP directamente ahí; deben quedar la DLL y `meta.json` en la raíz de esa carpeta.
5. Inicia Jellyfin.
6. Abre **Panel de control → Plugins → Private Playback**.
7. Confirma **Protección activa** y versión `10.11.11.0`.

Si el estado está inactivo, todos los usuarios conservan el comportamiento normal de Jellyfin. No des por supuesta la privacidad.

## Crear «Invitado» y activar privacidad completa

1. En **Panel de control → Usuarios**, crea `Invitado` y limita sus bibliotecas/permisos según necesites.
2. Abre la configuración de Private Playback.
3. Localiza `Invitado`; no escribas GUIDs manualmente.
4. Selecciona **Navegación privada completa**.
5. Guarda.
6. Cierra y vuelve a abrir la página para confirmar la política y el estado activo.

En modo personalizado puedes decidir si se permiten el progreso, el estado visto y el historial (contador + última fecha). Las combinaciones actúan sobre conceptos persistidos reales de Jellyfin 10.11.11.

## Borrar datos anteriores

Activar una política no toca el historial previo. Para borrarlo de forma explícita:

1. Pulsa **Vista previa de elementos afectados**.
2. Revisa la cantidad.
3. Pulsa **Eliminar datos de reproducción** y confirma.

La operación solo pone a cero posición/contador, elimina la última fecha y deja el elemento como no visto para ese usuario. Conserva favorito, valoración y pistas elegidas. Es idempotente e irreversible.

## Comprobación rápida

1. Reproduce parte de una película de más de cinco minutos como `Invitado` y detén la reproducción.
2. Consulta `GET /UserItems/{itemId}/UserData` autenticado como `Invitado`: la posición debe seguir en su valor anterior (cero si no había datos).
3. Ejecuta `POST /UserPlayedItems/{itemId}` con el token de `Invitado`.
4. Vuelve a consultar UserData: `Played` debe continuar en `false` si no había estado previo.
5. Reinicia Jellyfin, vuelve a iniciar sesión y repite la consulta.
6. Con un usuario normal, repite el marcado: su `Played` sí debe quedar en `true`.

El plan manual completo está en `docs/MANUAL_TEST_PLAN.md`.

## Desinstalación

Detén Jellyfin, retira la carpeta binaria del complemento y vuelve a iniciarlo. El core recupera su registro normal y los UserData existentes siguen siendo válidos. La desinstalación no restaura una limpieza solicitada ni elimina automáticamente la configuración XML.

## Limitaciones verificadas

- Solo Jellyfin 10.11.11 está declarado compatible.
- No se oculta el botón «Visto» y no se parchea jellyfin-web.
- No se impide el registro independiente de otros plugins.
- La matriz automatizada usa API real y media generada; no certifica visualmente cada cliente, transcodificador, acelerador o formato de subtítulo.
- El Quality Gate de SonarQube no se ejecutó localmente por falta de servidor/credenciales/Java 21; el workflow está preparado y no se afirma un resultado inexistente.
