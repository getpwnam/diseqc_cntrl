# HTTPS Transport

The REST API listens on TCP port 443 and permits TLS 1.2 through TLS 1.3 with an
ECDSA device certificate. The current firmware uses nanoFramework's stock Mbed
TLS configuration. It includes this preferred TLS 1.2 cipher suite:

`TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256`

The application uses P-256, SHA-256, and hardware-generated entropy. The stock
native configuration also compiles TLS 1.3, TLS client, RSA, and additional
cipher-suite support even though this REST listener does not request them.

TLS authenticates the server only. The API does not request client certificates
and does not provide application authentication or authorization. Network ACLs
remain necessary. A client that disables server-certificate validation is
encrypted against passive observation but remains vulnerable to interception.

## Provision A Device Credential

Generate an unencrypted P-256 key and a self-signed certificate for development:

```bash
openssl ecparam -name prime256v1 -genkey -noout -out cubley.key
openssl req -new -x509 -key cubley.key -sha256 -days 3650 \
  -subj "/CN=cubley.local" \
  -addext "subjectAltName=DNS:cubley.local,IP:<device-ip>" \
  -out cubley.crt
cat cubley.crt cubley.key > cubley-device.pem
```

Replace `<device-ip>` before running the command. Keep `cubley.key` and
`cubley-device.pem` secret. An unencrypted key keeps provisioning simple; the
configuration store itself must be protected as secret material.

In the nanoFramework VS Code Device Explorer, select the connected device and
use the device configuration command to update the **X.509 device certificate**.
Upload `cubley-device.pem`, which must contain the certificate followed by its
matching private key. This configuration is separate from managed application
deployment and persists across application updates.

Distribute only `cubley.crt` to API clients. For production, provision a unique
key per device and protect the provisioning workstation and retained backups.

## Client Test

Verify TLS 1.3:

```bash
curl --tlsv1.3 --tls-max 1.3 --cacert cubley.crt \
   "https://<device-ip>/api/v2/health"
```

Verify TLS 1.2 fallback and its preferred cipher suite:

```bash
curl --tlsv1.2 --tls-max 1.2 \
   --ciphers ECDHE-ECDSA-AES128-GCM-SHA256 \
   --cacert cubley.crt \
   "https://<device-ip>/api/v2/health"
```

Use `--insecure` only as a diagnostic to distinguish certificate trust failures
from handshake or application failures.

## Resource Measurements

Measurements are from Debug builds of CUBLEY_F407_0_5:

| Build | nanoCLR flash | 704 KiB region |
|---|---:|---:|
| Current stock Mbed TLS configuration | 696,696 bytes | 96.64% |
| Experimental TLS 1.2 single-suite profile | 458,848 bytes | 63.65% |
| Potential reduction from pruning | 237,848 bytes | 32.99 percentage points |

The managed CubleyControl deployment bundle is 188,088 bytes. Current link
inspection confirms that TLS 1.2 server and client code, TLS 1.3, RSA, PEM, and
the hardware entropy path are present. The single-suite result is retained as a
measured optimization option, not the active build configuration.

## Hardware Acceptance

Before release, verify on the board:

1. Provision the combined PEM credential, flash this firmware, and deploy the
   managed application using one deployment workflow.
2. Confirm the TLS 1.3 and TLS 1.2 `curl` commands both negotiate successfully.
3. Exercise health, state, command, and job endpoints over repeated connections.
4. Send stalled and malformed handshakes and confirm the two-second socket
   timeout returns the single connection worker to service.
5. Measure native and managed heap headroom and handshake latency during repeated
   connections, then record the results in the bring-up log.