# HTTPS Transport

The REST API listens on TCP port 443 and requires TLS 1.3 with an ECDSA device
certificate. The firmware uses nanoFramework's stock Mbed TLS configuration with
a target override that removes TLS 1.2, DTLS, and their legacy handshake options.

The application uses P-256, SHA-256, and hardware-generated entropy. The stock
native configuration also compiles TLS 1.3, TLS client, RSA, and additional
cipher-suite support even though this REST listener does not request them.

TLS authenticates the server only; the API does not request client certificates.
When an API token is configured, every REST request additionally requires that
bearer token. An unset token disables application authentication. Network ACLs
remain necessary, particularly when authentication is disabled. A client that
disables server-certificate validation can expose a configured token to an
active interceptor.

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

## Provision An API Token

Generate a token and stage it through the USB CDC configuration console:

```text
$ openssl rand -hex 32
cubley-a1b2c3> configure
cubley-a1b2c3(config)# api-token set <generated-token>
cubley-a1b2c3(config*)# commit
```

The token is stored in plaintext in the internal configuration sector and may be
shown in configuration output and command history. Diagnostic logs redact the
token value. When a token is committed, missing or incorrect credentials return
HTTP 401. Use
`api-token clear` followed by `commit` to disable authentication and permit
requests without an authorization header.

## Client Test

```bash
curl --tlsv1.3 --tls-max 1.3 --cacert cubley.crt \
   -H "Authorization: Bearer $CUBLEY_API_TOKEN" \
   "https://<device-ip>/api/v2/health"
```

Use `--insecure` only as a diagnostic to distinguish certificate trust failures
from handshake or application failures.

## Resource Measurements

Measurements are from Debug builds of CUBLEY_F407_0_5:

| Build | nanoCLR flash | 704 KiB region |
|---|---:|---:|
| Current TLS 1.3-only profile | 631,992 bytes | 87.67% |
| Stock TLS 1.2 and TLS 1.3 configuration | 696,696 bytes | 96.64% |
| Experimental TLS 1.2 single-suite profile | 458,848 bytes | 63.65% |

The managed CubleyControl deployment bundle is 190,728 bytes. Current link
inspection confirms that TLS 1.2 record and handshake code is absent while TLS
1.3, RSA, PEM, and the hardware entropy path remain present. The single-suite
result is retained as a measured optimization option, not the active build
configuration.

## Hardware Acceptance

Before release, verify on the board:

1. Provision the combined PEM credential, flash this firmware, and deploy the
   managed application using one deployment workflow.
2. Confirm the TLS 1.3 `curl` command negotiates successfully and a forced TLS
   1.2 connection is rejected.
3. Exercise health, state, command, and job endpoints over repeated connections.
4. Send stalled and malformed handshakes and confirm the two-second socket
   timeout returns the single connection worker to service.
5. Measure native and managed heap headroom and handshake latency during repeated
   connections, then record the results in the bring-up log.