# Custom public versions · 2026-10-30

Admin now has a visible Public version field and Release for public button regardless of candidate list availability. The owner can enter any unused canonical three-part version. Existing public versions cannot be replaced; withdrawn versions retain their original executable.

The Admin service writes a version/source-pinned request using its existing repository Contents read/write permission. GitHub builds a public executable stamped with the chosen version, checks it, starts the real interface and publishes only on successful verification. Status is read back into Admin. No token is written into repository requests.

Validation includes version normalization, exact bridge arguments, production service request/status handling, and a cloud verification-only build of v3.2.1. That test never publishes a new public version. Cloud run results will be recorded after completion. Physical M512/live CAD testing remains outstanding.
