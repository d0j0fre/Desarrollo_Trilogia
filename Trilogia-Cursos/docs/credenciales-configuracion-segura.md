# Credenciales y configuracion segura

## 1. Proposito

Esta politica mantiene secretos, cuentas demo y configuracion sensible fuera de Git. Aplica a desarrollo local, Azure, pruebas compartidas y documentacion.

## 2. Que se considera sensible

- Contrasenas y credenciales temporales.
- Connection strings completas.
- Tokens, API keys y cookies o tokens de sesion.
- Contrasenas SMTP.
- Publish profiles y capturas que revelen secretos.

## 3. Configuracion local

- Usar variables de entorno para valores locales y compartidos solo por canal privado.
- Usar .NET User Secrets cuando corresponda a desarrollo local.
- Mantener archivos locales ignorados fuera de Git.
- Conservar marcadores en `appsettings` versionados; nunca reemplazarlos con valores reales.
- Usar `<smtp-user>` y `<smtp-app-password>` en cualquier plantilla SMTP.

## 4. Configuracion en Azure

Los valores sensibles deben residir en App Service Configuration. Azure Key Vault puede incorporarse cuando el entorno lo requiera. Nunca se versionan secretos en archivos del proyecto.

## 5. Cuentas demo

- No documentar contrasenas funcionales ni pares reutilizables de usuario y contrasena.
- Crear o habilitar cuentas por un bloque aprobado.
- Compartir acceso temporal por un canal privado.
- Usar credenciales distintas entre local y Azure.
- Rotar o desactivar cuentas temporales al terminar las pruebas.
- Una contrasena expuesta en un seed publico no puede usarse en Azure.

## 6. Respuesta ante exposicion

1. Revocar o rotar la credencial.
2. Identificar el alcance de la exposicion.
3. Retirar el valor del estado actual.
4. Verificar aplicaciones y variables de configuracion.
5. Revisar logs y accesos relevantes.
6. Evaluar la limpieza de historial por separado.
7. Coordinar a todos los colaboradores antes de reescribir historial.

Eliminar una credencial de un archivo no la invalida.

## 7. Reglas para Codex

- Nunca imprimir secretos.
- Nunca anadir secretos a commits.
- Usar `[SECRETO DETECTADO]` al reportar un hallazgo.
- Detenerse ante una credencial inesperada.
- No modificar App Service Configuration sin autorizacion explicita.
- No ejecutar scripts de rotacion sin aprobacion.

## 8. Checklist antes de un PR

- Revisar `git diff` y archivos nuevos.
- Revisar configuracion y perfiles de publicacion.
- Revisar documentacion y SQL.
- Ejecutar un escaneo de secretos.
- Compilar la solucion.

## 9. Escaneo automatizado

Desde la raiz Git, ejecutar:

```powershell
pwsh ./Trilogia-Cursos/scripts/security/Test-RepositorySecrets.ps1
```

El escaneo revisa solo archivos rastreados, no imprime valores sensibles y falla cuando detecta una exposicion real. Los placeholders aprobados, como `<demo-password>`, `<sql-password>` y `<smtp-app-password>`, se permiten solo como ejemplos no funcionales.
