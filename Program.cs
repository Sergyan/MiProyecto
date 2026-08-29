using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configurar SQLite
builder.Services.AddDbContext<HL7Context>(options =>
    options.UseSqlite("Data Source=hl7_data.db"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Crear base de datos
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HL7Context>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseStaticFiles(); // Para servir la UI web

// =====================
// API ENDPOINTS
// =====================

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    timestampUtc = DateTime.UtcNow
}));

app.MapGet("/api/hl7", async (HL7Context db, int? skip = 0, int? take = 100) =>
{
    var results = await db.HL7Messages
        .AsNoTracking()
        .OrderByDescending(m => m.ReceivedAtUtc)
        .Skip(skip ?? 0)
        .Take(take ?? 100)
        .ToListAsync();
    
    return Results.Ok(new { total = db.HL7Messages.Count(), data = results });
});

app.MapGet("/api/hl7/latest", async (HL7Context db) =>
{
    var latest = await db.HL7Messages
        .AsNoTracking()
        .OrderByDescending(m => m.ReceivedAtUtc)
        .FirstOrDefaultAsync();
    
    return latest != null 
        ? Results.Ok(latest) 
        : Results.NotFound(new { message = "No frames have been received yet." });
});

app.MapGet("/api/hl7/{id}", async (string id, HL7Context db) =>
{
    var frame = await db.HL7Messages
        .AsNoTracking()
        .Include(m => m.TestResults)
        .FirstOrDefaultAsync(m => m.Id == id);
    
    return frame != null 
        ? Results.Ok(frame) 
        : Results.NotFound(new { message = $"Frame {id} not found." });
});

app.MapGet("/api/hl7/patient/{patientId}", async (string patientId, HL7Context db) =>
{
    var results = await db.HL7Messages
        .AsNoTracking()
        .Include(m => m.TestResults)
        .Where(m => m.PatientId == patientId)
        .OrderByDescending(m => m.ReceivedAtUtc)
        .ToListAsync();
    
    return Results.Ok(new { patientId, count = results.Count, data = results });
});

app.MapGet("/api/hl7/search", async (HL7Context db, string? patientId, string? observationId, string? abnormalOnly) =>
{
    var query = db.HL7Messages.AsNoTracking();
    
    if (!string.IsNullOrEmpty(patientId))
        query = query.Where(m => m.PatientId.Contains(patientId));
    
    var results = await query.OrderByDescending(m => m.ReceivedAtUtc).ToListAsync();
    
    if (!string.IsNullOrEmpty(observationId))
    {
        var obsId = observationId.ToLower();
        results = results.Where(m => 
            m.TestResults.Any(t => t.ObservationIdentifier.ToLower().Contains(obsId))
        ).ToList();
    }
    
    if (abnormalOnly?.ToLower() == "true")
    {
        results = results.Where(m =>
            m.TestResults.Any(t => !string.IsNullOrEmpty(t.AbnormalFlag) && t.AbnormalFlag != "")
        ).ToList();
    }
    
    return Results.Ok(new { count = results.Count, data = results });
});

app.MapPost("/api/hl7/receive", async (FrameRequest request, HL7Context db) =>
{
    if (string.IsNullOrWhiteSpace(request.Frame))
    {
        return Results.BadRequest(new { message = "The 'frame' field is required." });
    }

    var type = DetectFrameType(request.Frame);
    var parsed = ParseFrame(request.Frame, type);
    
    var stored = new HL7Message
    {
        Id = Guid.NewGuid().ToString("N"),
        Type = type,
        Emitter = request.Emitter,
        ReceivedAtUtc = DateTime.UtcNow,
        RawFrame = request.Frame.Trim(),
        MessageType = parsed.messageHeader?.MessageType ?? "",
        SendingApplication = parsed.messageHeader?.SendingApplication ?? "",
        ReceivingApplication = parsed.messageHeader?.ReceivingApplication ?? "",
        MessageControlId = parsed.messageHeader?.MessageControlId ?? "",
        PatientId = parsed.patientInfo?.PatientId ?? "",
        PatientName = parsed.patientInfo?.PatientName ?? "",
        DateOfBirth = parsed.patientInfo?.DateOfBirth ?? "",
        Sex = parsed.patientInfo?.Sex ?? "",
        TestResults = parsed.observations.Select(o => new TestResult
        {
            SetId = o.SetId,
            ValueType = o.ValueType,
            ObservationIdentifier = o.ObservationIdentifier,
            Value = o.Value,
            Units = o.Units,
            ReferenceRange = o.ReferenceRange,
            AbnormalFlag = o.AbnormalFlag,
            ResultStatus = o.ResultStatus
        }).ToList()
    };

    db.HL7Messages.Add(stored);
    await db.SaveChangesAsync();

    var segments = stored.RawFrame
        .Replace("\r", "\n")
        .Replace("\n\n", "\n")
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    Console.WriteLine("====================================================");
    Console.WriteLine($"[HL7 RECEIVED] Type: {stored.Type} | Emitter: {stored.Emitter} | Time: {stored.ReceivedAtUtc:O}");
    Console.WriteLine($"Total Observations: {stored.TestResults.Count}");
    Console.WriteLine();
    
    Console.WriteLine("  === MESSAGE HEADER ===");
    Console.WriteLine($"    Message Type:        {stored.MessageType}");
    Console.WriteLine($"    Sending App:         {stored.SendingApplication}");
    Console.WriteLine($"    Receiving App:       {stored.ReceivingApplication}");
    Console.WriteLine($"    Message Control ID:  {stored.MessageControlId}");
    Console.WriteLine();
    
    Console.WriteLine("  === PATIENT INFO ===");
    Console.WriteLine($"    Patient ID:          {stored.PatientId}");
    Console.WriteLine($"    Patient Name:        {stored.PatientName}");
    Console.WriteLine($"    Date of Birth:       {stored.DateOfBirth}");
    Console.WriteLine($"    Sex:                 {stored.Sex}");
    Console.WriteLine();

    if (stored.TestResults.Count > 0)
    {
        Console.WriteLine("  === TEST RESULTS (OBSERVATIONS) ===");
        foreach (var obs in stored.TestResults.OrderBy(o => o.SetId).Take(10))
        {
            var status = obs.AbnormalFlag switch
            {
                "L" => " [LOW]",
                "H" => " [HIGH]",
                _ => ""
            };
            var refRange = string.IsNullOrWhiteSpace(obs.ReferenceRange) ? "" : $" (Ref: {obs.ReferenceRange})";
            Console.WriteLine($"    [{obs.SetId:D2}] {obs.ObservationIdentifier,-25} {obs.Value,-10} {obs.Units,-12}{refRange}{status}");
        }
        if (stored.TestResults.Count > 10)
            Console.WriteLine($"    ... and {stored.TestResults.Count - 10} more observations");
    }
    Console.WriteLine("====================================================");

    return Results.Ok(stored);
});

// =====================
// UI WEB - PÁGINA PRINCIPAL
// =====================
app.MapGet("/", () => Results.Content(GetHtmlUI(), "text/html"));

app.Run();

// =====================
// FUNCIONES AUXILIARES
// =====================

static string DetectFrameType(string frame)
{
    var normalized = frame.Trim();
    if (normalized.StartsWith("MSH", StringComparison.OrdinalIgnoreCase))
        return "HL7";
    if (normalized.Contains("ASTM", StringComparison.OrdinalIgnoreCase) || normalized.Contains("\u0002") || normalized.Contains("\u0003"))
        return "ASTM";
    return "UNKNOWN";
}

static (MessageHeader? messageHeader, PatientInfo? patientInfo, List<ObservationDto> observations) ParseFrame(string frame, string type)
{
    var messageHeader = new MessageHeader();
    var patientInfo = new PatientInfo();
    var observations = new List<ObservationDto>();

    if (string.IsNullOrWhiteSpace(frame)) 
        return (messageHeader, patientInfo, observations);

    if (type == "HL7")
    {
        var segments = frame.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        
        foreach (var segment in segments)
        {
            if (segment.StartsWith("MSH", StringComparison.OrdinalIgnoreCase))
            {
                var fields = segment.Split('|');
                messageHeader.MessageType = fields.Length > 8 ? fields[8] : "";
                messageHeader.SendingApplication = fields.Length > 2 ? fields[2] : "";
                messageHeader.ReceivingApplication = fields.Length > 3 ? fields[3] : "";
                messageHeader.MessageControlId = fields.Length > 9 ? fields[9] : "";
            }
            else if (segment.StartsWith("PID", StringComparison.OrdinalIgnoreCase))
            {
                var fields = segment.Split('|');
                patientInfo.PatientId = fields.Length > 3 ? fields[3] : "";
                patientInfo.PatientName = fields.Length > 4 ? fields[4] : "";
                patientInfo.DateOfBirth = fields.Length > 6 ? fields[6] : "";
                patientInfo.Sex = fields.Length > 7 ? fields[7] : "";
            }
            else if (segment.StartsWith("OBX", StringComparison.OrdinalIgnoreCase))
            {
                var fields = segment.Split('|');
                if (fields.Length > 1 && int.TryParse(fields[1], out var setId))
                {
                    observations.Add(new ObservationDto
                    {
                        SetId = setId,
                        ValueType = fields.Length > 2 ? fields[2] : "",
                        ObservationIdentifier = fields.Length > 3 ? fields[3] : "",
                        Value = fields.Length > 5 ? fields[5] : "",
                        Units = fields.Length > 6 ? fields[6] : "",
                        ReferenceRange = fields.Length > 7 ? fields[7] : "",
                        AbnormalFlag = fields.Length > 8 ? fields[8] : "",
                        ResultStatus = fields.Length > 11 ? fields[11] : ""
                    });
                }
            }
        }
    }

    return (messageHeader, patientInfo, observations);
}

static string GetHtmlUI() => """
<!DOCTYPE html>
<html lang="es">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>HL7 Lab Viewer</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background: #f5f5f5; color: #333; }
        .container { max-width: 1200px; margin: 0 auto; padding: 20px; }
        header { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px 0; margin-bottom: 30px; border-radius: 8px; }
        header h1 { font-size: 2.5em; margin-bottom: 10px; }
        header p { font-size: 1.1em; opacity: 0.9; }
        .search-box { background: white; padding: 25px; border-radius: 8px; margin-bottom: 30px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }
        .search-box h2 { margin-bottom: 20px; font-size: 1.3em; color: #667eea; }
        .search-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 15px; margin-bottom: 15px; }
        input, select { width: 100%; padding: 10px 12px; border: 1px solid #ddd; border-radius: 4px; font-size: 0.95em; }
        input:focus, select:focus { outline: none; border-color: #667eea; box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1); }
        button { background: #667eea; color: white; padding: 10px 25px; border: none; border-radius: 4px; cursor: pointer; font-weight: 600; transition: background 0.3s; }
        button:hover { background: #764ba2; }
        .results { background: white; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); overflow: hidden; }
        .result-item { border-bottom: 1px solid #eee; padding: 20px; transition: background 0.2s; cursor: pointer; }
        .result-item:hover { background: #f9f9f9; }
        .result-item:last-child { border-bottom: none; }
        .patient-info { display: grid; grid-template-columns: 1fr 1fr; gap: 15px; margin-bottom: 15px; }
        .info-block { background: #f0f4ff; padding: 12px; border-left: 4px solid #667eea; border-radius: 4px; }
        .info-label { font-weight: 600; color: #667eea; font-size: 0.85em; text-transform: uppercase; }
        .info-value { font-size: 1.1em; margin-top: 4px; }
        .obs-table { width: 100%; border-collapse: collapse; margin-top: 15px; }
        .obs-table th { background: #667eea; color: white; padding: 12px; text-align: left; font-weight: 600; }
        .obs-table td { padding: 10px 12px; border-bottom: 1px solid #eee; }
        .obs-table tr:nth-child(even) { background: #f9f9f9; }
        .status-low { color: #ff6b6b; font-weight: 600; }
        .status-high { color: #ff922b; font-weight: 600; }
        .status-normal { color: #51cf66; }
        .loading { text-align: center; padding: 40px; color: #999; }
        .error { background: #ffe0e0; color: #c92a2a; padding: 15px; border-radius: 4px; margin-bottom: 20px; }
        .success { background: #d3f9d8; color: #2f9e44; padding: 15px; border-radius: 4px; margin-bottom: 20px; }
    </style>
</head>
<body>
    <div class="container">
        <header>
            <h1>🔬 HL7 Lab Viewer</h1>
            <p>Visualiza y busca resultados de laboratorio en tiempo real</p>
        </header>

        <div class="search-box">
            <h2>Búsqueda de Resultados</h2>
            <div class="search-grid">
                <div>
                    <label>ID del Paciente</label>
                    <input type="text" id="patientId" placeholder="Ej: jimmi">
                </div>
                <div>
                    <label>Observación/Parámetro</label>
                    <input type="text" id="obsId" placeholder="Ej: GLUCOSE, Na+">
                </div>
                <div>
                    <label>Mostrar Solo Anormales</label>
                    <select id="abnormal">
                        <option value="">Todos los resultados</option>
                        <option value="true">Solo anormales</option>
                    </select>
                </div>
            </div>
            <button onclick="search()">🔍 Buscar</button>
        </div>

        <div id="message" style="display: none;"></div>
        <div id="results" class="results"></div>
    </div>

    <script>
        async function search() {
            const patientId = document.getElementById('patientId').value;
            const obsId = document.getElementById('obsId').value;
            const abnormal = document.getElementById('abnormal').value;
            const resultsDiv = document.getElementById('results');
            const messageDiv = document.getElementById('message');
            
            messageDiv.style.display = 'none';
            resultsDiv.innerHTML = '<div class="loading">Cargando...</div>';

            try {
                const url = new URL('/api/hl7/search', window.location.origin);
                if (patientId) url.searchParams.append('patientId', patientId);
                if (obsId) url.searchParams.append('observationId', obsId);
                if (abnormal) url.searchParams.append('abnormalOnly', abnormal);

                const response = await fetch(url);
                const data = await response.json();

                if (data.count === 0) {
                    resultsDiv.innerHTML = '<div class="loading">No se encontraron resultados</div>';
                    return;
                }

                resultsDiv.innerHTML = data.data.map(msg => `
                    <div class="result-item">
                        <div class="patient-info">
                            <div class="info-block">
                                <div class="info-label">Paciente</div>
                                <div class="info-value">${msg.patientId || 'N/A'}</div>
                            </div>
                            <div class="info-block">
                                <div class="info-label">Nombre</div>
                                <div class="info-value">${msg.patientName || 'N/A'}</div>
                            </div>
                            <div class="info-block">
                                <div class="info-label">Fecha de Recepción</div>
                                <div class="info-value">${new Date(msg.receivedAtUtc).toLocaleString('es-ES')}</div>
                            </div>
                            <div class="info-block">
                                <div class="info-label">Tipo de Mensaje</div>
                                <div class="info-value">${msg.messageType}</div>
                            </div>
                        </div>
                        <table class="obs-table">
                            <thead>
                                <tr>
                                    <th>#</th>
                                    <th>Parámetro</th>
                                    <th>Valor</th>
                                    <th>Unidad</th>
                                    <th>Rango Normal</th>
                                    <th>Estado</th>
                                </tr>
                            </thead>
                            <tbody>
                                ${msg.testResults.map(obs => {
                                    let statusClass = 'status-normal';
                                    let statusText = '✓ Normal';
                                    if (obs.abnormalFlag === 'H') { statusClass = 'status-high'; statusText = '⚠ Alto'; }
                                    else if (obs.abnormalFlag === 'L') { statusClass = 'status-low'; statusText = '⚠ Bajo'; }
                                    
                                    return `
                                        <tr>
                                            <td>${obs.setId}</td>
                                            <td>${obs.observationIdentifier}</td>
                                            <td><strong>${obs.value}</strong></td>
                                            <td>${obs.units}</td>
                                            <td>${obs.referenceRange || '—'}</td>
                                            <td><span class="${statusClass}">${statusText}</span></td>
                                        </tr>
                                    `;
                                }).join('')}
                            </tbody>
                        </table>
                    </div>
                `).join('');
            } catch (error) {
                messageDiv.className = 'error';
                messageDiv.textContent = 'Error: ' + error.message;
                messageDiv.style.display = 'block';
                resultsDiv.innerHTML = '';
            }
        }

        // Cargar últimos resultados al iniciar
        window.addEventListener('load', search);
    </script>
</body>
</html>
""";

// =====================
// DTOs Y MODELOS
// =====================

public class FrameRequest
{
    public string Frame { get; set; } = string.Empty;
    public string Emitter { get; set; } = "unknown";
}

public class MessageHeader
{
    public string MessageType { get; set; } = "";
    public string SendingApplication { get; set; } = "";
    public string ReceivingApplication { get; set; } = "";
    public string MessageControlId { get; set; } = "";
}

public class PatientInfo
{
    public string PatientId { get; set; } = "";
    public string PatientName { get; set; } = "";
    public string DateOfBirth { get; set; } = "";
    public string Sex { get; set; } = "";
}

public class ObservationDto
{
    public int SetId { get; set; }
    public string ValueType { get; set; } = "";
    public string ObservationIdentifier { get; set; } = "";
    public string Value { get; set; } = "";
    public string Units { get; set; } = "";
    public string ReferenceRange { get; set; } = "";
    public string AbnormalFlag { get; set; } = "";
    public string ResultStatus { get; set; } = "";
}

// =====================
// ENTIDADES DE BASE DE DATOS
// =====================

public class HL7Message
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Emitter { get; set; } = "";
    public DateTime ReceivedAtUtc { get; set; }
    public string RawFrame { get; set; } = "";
    
    // Message Header
    public string MessageType { get; set; } = "";
    public string SendingApplication { get; set; } = "";
    public string ReceivingApplication { get; set; } = "";
    public string MessageControlId { get; set; } = "";
    
    // Patient Info
    public string PatientId { get; set; } = "";
    public string PatientName { get; set; } = "";
    public string DateOfBirth { get; set; } = "";
    public string Sex { get; set; } = "";
    
    // Relación con resultados
    public List<TestResult> TestResults { get; set; } = new();
}

public class TestResult
{
    public int Id { get; set; }
    public int SetId { get; set; }
    public string ValueType { get; set; } = "";
    public string ObservationIdentifier { get; set; } = "";
    public string Value { get; set; } = "";
    public string Units { get; set; } = "";
    public string ReferenceRange { get; set; } = "";
    public string AbnormalFlag { get; set; } = "";
    public string ResultStatus { get; set; } = "";
    
    // Foreign key
    public string HL7MessageId { get; set; } = "";
    public HL7Message? HL7Message { get; set; }
}

// =====================
// DBCONTEXT
// =====================

public class HL7Context : DbContext
{
    public DbSet<HL7Message> HL7Messages { get; set; }
    public DbSet<TestResult> TestResults { get; set; }

    public HL7Context(DbContextOptions<HL7Context> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<HL7Message>()
            .HasKey(m => m.Id);

        modelBuilder.Entity<TestResult>()
            .HasKey(r => r.Id);

        modelBuilder.Entity<TestResult>()
            .HasOne(r => r.HL7Message)
            .WithMany(m => m.TestResults)
            .HasForeignKey(r => r.HL7MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
