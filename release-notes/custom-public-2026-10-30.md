# Custom public versions · 2026-10-30

Admin now has a visible Public version field and Release for public button regardless of candidate list availability. The owner can enter any unused canonical three-part version. Existing public versions cannot be replaced; withdrawn versions retain their original executable.

The Admin service writes a version/source-pinned request using its existing repository Contents read/write permission. GitHub builds a public executable stamped with the chosen version, checks it, starts the real interface and publishes only on successful verification. Status is read back into Admin. No token is written into repository requests.

Validation includes version normalization, exact bridge arguments, production service request/status handling, and a cloud verification-only build of v3.2.1. That test never publishes a new public version. Both cloud runs passed; the results are recorded below. Physical M512/live CAD testing remains outstanding.

Verified 2026-10-30: [Admin/public build run 41077895396](https://github.com/chamfer3dprinting/Chamfer-Trackball/actions/runs/41077895396) passed 108 core checks, UI/bridge assertions and both actual app smoke tests. [Chosen-version run 41077895521](https://github.com/chamfer3dprinting/Chamfer-Trackball/actions/runs/41077895521) stamped a public executable as v3.2.1, passed the same checks and its actual interface test, then exited in verification-only mode without publishing. Source: `7de63888bbef561e75b5fb85f9def74df3362c8f`. Admin v1.5.8 SHA-256: `251d126d3e91cddc5f7d333969573d0973e2b480019bcabc179e366df875b5ec`. The live stable public release remains v1.0.1. The standalone Admin publishing connection is still separate from the Codex GitHub connection.
