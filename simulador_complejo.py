#!/usr/bin/env python3
"""
Simulador complejo: Envía un mensaje HL7 de análisis de gases en sangre
con múltiples observaciones (OBX) de un analizador de laboratorio.
"""

import requests
import json

# URL de la API
API_URL = "http://localhost:5075/api/hl7/receive"

# Mensaje HL7 complejo - Análisis de gases en sangre
HL7_MESSAGE = r"""MSH|^~\&|epoc|Epocal|LAB|LAB|20260817171802||ORU^R01|2026081209191445080|P|2.6|||AL|NE|||||||||
PID|1||jimmi||||||||||||||||||||||||||||||||||||
OBR|1|||BGEM^BGEM Test Card|||20260812091914|||||||20260812091914|Blood|||||||||||||||||||oncorad||||||||||||||||
OBX|1|NM|BE(b)||-2.5|mmol/L|-2.0-3.0|L|||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|2|NM|BE(ecf)||-3.2|mmol/L|-2.0-3.0|L|||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|3|NM|BUN||7|mg/dL|8-26|L|||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|4|NM|Ca++||0.51|mmol/L|1.15-1.33|L|||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|5|NM|Cl-||98|mmol/L|98-107||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|6|NM|Crea||0.62|mg/dL|0.51-1.19||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|7|NM|GLU^8080808||291|mg/dL|74-100|H|||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|8|NM|Hct||33|%|38-51|L|||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|9|NM|K+||3.8|mmol/L|3.5-4.5||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|10|NM|Lac||1.94|mmol/L|0.36-0.75|H|||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|11|NM|Na+||126|mmol/L|138-146|L|||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|12|NM|mTCO2||20.1|mmol/L|22.0-29.0|L|||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|13|NM|cHCO3-||21.2|mmol/L|21.0-28.0||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|14|NM|cHgb||11.3|g/dL|12.0-17.0|L|||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|15|NM|cSO2||99.8|%|94.0-98.0|H|||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|16|NM|pCO2||32.1|mmHg|35.0-48.0|L|||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|17|NM|pH||7.427||7.350-7.450||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|18|NM|pO2||207.0|mmHg|83.0-108.0|H|||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|19|ST|Hemodilution||S||||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|20|ST|Sample type||Unspecified||||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|21|ST|Criticals present||No||||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|22|NM|Ambient temperature||72.1|F|||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|23|NM|Ambient pressure||759.3|mmHg|||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|24|ST|EDM Test status||OK||||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|25|ST|Card Lot||02-26062-30||||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|26|ST|Card Expiration Date||20260818||||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|27|ST|ReaderSerNum||45080||||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|28|ST|HostSerNum||T1H13XQB00530||||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|29|ST|HostAlias||T1H13XQB00530||||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|30|ST|Reader Alias||Rdr45080||||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|31|ST|Department name||Default||||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|32|ST|ReaderMaintenanceRequired||No||||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|33|NM|Bubble width||1.15||||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|34|ST|EnforceCriticalHandling||Yes||||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|35|ST|Host Mode||0||||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|36|ST|eQC time||12-Aug-2026 09:15:46||||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|37|NM|Test duration||225.2||||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||
OBX|38|ST|Host SW Version||4.17.6||||||F|||20260812091914||||^^45080~T1H13XQB00530|||||||"""

# Payload para enviar
payload = {
    "frame": HL7_MESSAGE,
    "emitter": "simulador-complejo"
}

try:
    response = requests.post(API_URL, json=payload)
    print(f"Status: {response.status_code}")
    if response.status_code == 200:
        data = response.json()
        print(json.dumps(data, indent=2))
    else:
        print(f"Error: {response.text}")
except Exception as e:
    print(f"Exception: {e}")
