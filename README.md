# MiProyecto

Proyecto de prueba y validación de mensajes HL7/ASTM para laboratorio, con almacenamiento en SQLite y una interfaz web embebida para consultar resultados.

## Objetivo

Este proyecto permite:

- recibir tramas HL7/ASTM desde un emisor externo o simulador
- parsearlas y extraer información del paciente y observaciones
- guardar los datos en SQLite
- consultar resultados por paciente, parámetro u observación
- mostrar la información en una interfaz web simple dentro del mismo proyecto

## Estado actual

La aplicación ya está funcionando en una versión funcional con las siguientes capacidades:

- API ASP.NET Core mínima
- base de datos SQLite
- parsing de mensajes HL7 con segmentos MSH, PID y OBX
- almacenamiento de observaciones por resultado
- filtros por paciente, parámetro y solo anormales
- interfaz web embebida para consultar resultados
- limpieza automática de datos previos al iniciar la app para evitar duplicados
- botón de limpieza desde la GUI

## Arquitectura

- Backend: ASP.NET Core / C#
- Base de datos: SQLite
- Persistencia: Entity Framework Core
- Interfaz: HTML + CSS + JavaScript embebida en la misma app

## Estructura de la solución

- `Program.cs`: lógica principal, API, parser y UI web
- `hl7_data.db`: base de datos local generada por SQLite
- `MiProyecto.csproj`: configuración del proyecto

## Guía paso a paso

### 1. Preparar el entorno Python

Si vas a usar el simulador de pruebas o scripts auxiliares en Python, es recomendable crear un entorno virtual.

```bash
python -m venv .venv
.venv\Scripts\activate
python -m pip install --upgrade pip
```

Este entorno sirve para aislar librerías de Python y evitar mezclar dependencias del sistema con las del proyecto.

Importante: un entorno virtual no es una solución para un bloqueo de seguridad del sistema operativo. Sirve para Python, no para saltarse políticas de Windows como Device Guard, Memory Integrity o Code Integrity.

### 2. Preparar el entorno .NET

Instala el SDK de .NET que necesite el proyecto y comprueba que se puede ejecutar.

```bash
dotnet --version
dotnet restore
dotnet build
```

Si la app no inicia, revisa primero si el problema es del sistema, no del código.

### 3. Ejecutar la API

Desde la carpeta del proyecto:

```bash
dotnet run
```

La aplicación queda disponible en:

```text
http://localhost:5075
```

### 4. Ejecutar el simulador

Si tienes un script Python para enviar tramas HL7/ASTM, actívalo desde el entorno virtual:

```bash
python simulador.py
```

o

```bash
python simulador_complejo.py
```

Esto envía frames a la API y la app los guarda en SQLite.

### 5. Ver la interfaz web

Abre la URL local en el navegador y comprueba que la pantalla empieza vacía por defecto.

La GUI:

- se queda vacía si no hay filtros activos
- se puede buscar por paciente, parámetro o solo anormales
- incluye un botón para limpiar datos si quieres borrar el contenido actual

### 6. Qué hemos validado funcionalmente

Hasta aquí se ha comprobado que:

- el proyecto compila correctamente
- la API arranca en localhost
- el simulador envía tramas reales
- la app guarda los mensajes en SQLite
- la GUI muestra los resultados filtrados
- los datos previos no se acumulan de forma absurda al reiniciar la app
- la hora se muestra en formato local coherente con el equipo

### 7. Problema de certificados y seguridad del sistema

En algunos equipos, sobre todo con Windows y políticas de seguridad más estrictas, puede aparecer un bloqueo al ejecutar o firmar binarios .NET.

Esto no significa que el proyecto esté mal. Normalmente apunta a:

- Device Guard
- Memory Integrity
- Code Integrity
- políticas de seguridad del equipo
- certificado no válido o no reconocido

Si aparece un error de certificado al intentar importar un `.p12` o `.pfx`, se recomienda revisar:

- si la contraseña del certificado es correcta
- si el archivo es realmente un PFX válido
- si el certificado está bien exportado
- si el equipo tiene políticas que impiden la ejecución aunque el certificado sea correcto

Si falla la firma o la ejecución, el punto clave es que esto puede estar más relacionado con el entorno de Windows que con la app.

En esos casos, la recomendación práctica es:

- comprobar si el sistema tiene políticas de seguridad activas
- reiniciar el equipo y probar de nuevo
- confirmar que el certificado importa correctamente
- si sigue fallando, asumir que la causa es de entorno y no de proyecto

### 8. Nota sobre la memoria y la política del equipo

Durante el desarrollo apareció un problema que parecía del proyecto, pero realmente terminaba siendo un bloqueo del sistema operativo. Por eso es importante recordar:

- la parte Python no sirve para evadir políticas de seguridad
- la parte .NET no está “rota” si compila y luego falla por seguridad del sistema
- la app puede estar bien, pero el equipo puede impedir su ejecución por política

## Cómo ejecutar la aplicación (resumen)

```bash
dotnet restore
dotnet build
dotnet run
```

Y abrir en:

```text
http://localhost:5075
```

## Endpoints principales

### Estado de la aplicación

```http
GET /health
```

### Búsqueda por filtros

```http
GET /api/hl7/search?patientId=jimmi&observationId=GLUCOSE&abnormalOnly=true
```

### Recepción de trama HL7

```http
POST /api/hl7/receive
Content-Type: application/json

{
  "frame": "MSH|^~\\&|...",
  "emitter": "simulator"
}
```

### Reset de la base de datos

```http
POST /api/hl7/reset
```

## Consideraciones

- La API está pensada para uso local y pruebas de laboratorio.
- La base de datos se limpia al arrancar para mantener la vista vacía por defecto.
- Los filtros permiten consultar solo resultados relevantes sin mostrar registros antiguos o no deseados.
- En equipos con políticas de seguridad estrictas de Windows, la ejecución del binario .NET puede verse bloqueada por el sistema operativo; esto no es un problema de la aplicación en sí, sino de la política del entorno.

## Siguientes pasos recomendados

- conectar la API a un cliente externo real
- montar una interfaz web separada para consumo de la base de datos
- añadir autenticación y control de usuarios
- preparar despliegue en entorno real
- ampliar el parser para más variantes HL7

## Autor

Sergio Rubio
Proyecto desarrollado en entorno local para pruebas funcionales y validación técnica.
