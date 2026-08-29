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

## Cómo ejecutar

1. Clona el repositorio
2. Entra en la carpeta del proyecto
3. Ejecuta:

```bash
dotnet restore
dotnet build
dotnet run
```

4. Abre la aplicación en:

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

Proyecto desarrollado en entorno local para pruebas funcionales y validación técnica.
