import json
import sys
import urllib.request
import urllib.error

API_URL = "http://localhost:5075/api/hl7/receive"

# Muestra un ejemplo de trama HL7 realista
FRAME = (
    "MSH|^~\\&|LAB1|HOSPITAL|CLIA|LAB|20260829||ORM^O01|MSG123|P|2.3\r"
    "PID|1||123456|DOE^JOHN||19880101|M\r"
    "OBX|1|NM|GLUCOSE^Glucose|1|95|mg/dL|70-110|N\r"
)

payload = json.dumps({
    "emitter": "simulador-local",
    "frame": FRAME,
}).encode("utf-8")

req = urllib.request.Request(
    API_URL,
    data=payload,
    headers={"Content-Type": "application/json"},
    method="POST",
)

try:
    with urllib.request.urlopen(req, timeout=10) as response:
        body = response.read().decode("utf-8")
        print(f"Status: {response.status}")
        print(body)
except urllib.error.HTTPError as e:
    print(f"Error HTTP {e.code}: {e.read().decode('utf-8', 'ignore')}")
    sys.exit(1)
except Exception as exc:
    print(f"No se pudo conectar con la API: {exc}")
    print("Asegúrate de ejecutar: dotnet run --urls http://localhost:5075")
    sys.exit(1)
