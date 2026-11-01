# Feature Specification: Local Jenkins CI/CD on WSL2

**Feature Branch**: `010-wsl2-jenkins-cicd`

**Created**: 2026-11-01

**Status**: Draft

**Input**: User description: "help me to add Jenkin in wls2. create a CI/CD for me. In CI, I can choose the branch that I want to deploy. In the CD, I want deploy on k3s in that mechine"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run isolated checks on a chosen branch (Priority: P1)

A developer on the same WSL2 machine opens the local Jenkins host, picks the git branch they want, and starts CI. CI checks out that branch and runs the existing isolated Catalog and building-block checks. Those checks do **not** start the local cluster and do **not** change what is already running on it.

**Why this priority**: “Choose the branch” is not delivered until the operator can verify that exact branch without touching the live cluster.

**Independent Test**: With the local cluster stopped, start a CI run for a known-good branch and confirm isolated checks pass. Start a CI run for a missing or broken branch and confirm CI fails and the cluster (if later started) is unchanged.

**Acceptance Scenarios**:

1. **Given** Jenkins is running on WSL2 and the repository is available, **When** the operator starts CI and selects an existing branch, **Then** CI checks out that branch and runs the existing isolated test suites.
2. **Given** CI is running, **When** isolated checks execute, **Then** they MUST NOT start k3s and MUST NOT apply or delete cluster resources.
3. **Given** the operator selects a branch that does not exist, **When** CI starts, **Then** the run fails with a clear “branch not found” result and CD does not start.
4. **Given** isolated checks fail on the chosen branch, **When** the run finishes, **Then** CD MUST NOT deploy and the cluster Catalog that was already running (if any) stays on its previous revision.

---

### User Story 2 - Deploy the chosen branch onto this machine’s k3s (Priority: P1)

After CI passes for the chosen branch, CD deploys **that same branch** onto the k3s cluster already used for local product work on **this** WSL2 machine. When CD finishes, Catalog on the documented local URL serves the chosen branch. Create-product and get-by-id still work. Catalog Ready still waits only on the catalog store.

**Why this priority**: The user asked for CD onto k3s on the same machine. CI without that deploy is incomplete.

**Independent Test**: Pick a branch that passes isolated checks. Run the documented pipeline. After CD succeeds, create a product and get it by id on the documented Catalog URL. Confirm Catalog is Ready even if search or the viewer are still starting.

**Acceptance Scenarios**:

1. **Given** CI passed for branch B, **When** CD runs, **Then** the local k3s cluster hosts Catalog built from branch B (not from a different branch).
2. **Given** CD has succeeded, **When** a client calls create-product then get-by-id on the documented local Catalog URL, **Then** both succeed against the cluster catalog store.
3. **Given** search or the viewer are not Ready, **When** CD finishes Catalog and the store, **Then** Catalog MUST still appear Ready and accept create and get-by-id.
4. **Given** CD is deploying, **When** it updates the cluster, **Then** it MUST NOT wipe the cluster catalog store as the default action (existing products stay unless the operator later chooses a documented destructive reset outside this pipeline).

---

### User Story 3 - Documented Jenkins on this WSL2 machine (Priority: P2)

A developer who already has k3s in Ubuntu on WSL2 follows project run notes to install and start Jenkins **on that same machine**. They open the Jenkins UI from Windows, see the eShop pipeline, pick a branch, and run CI then CD. They do not need a second computer, a cloud Jenkins, or a cloud cluster.

**Why this priority**: “Add Jenkins in WSL2” is not usable until start/stop and the branch picker are documented on this laptop.

**Independent Test**: Follow the run notes on a machine that already has k3s. Open Jenkins, start a pipeline, see a branch field, run CI. Isolated `dotnet test` still passes with Jenkins stopped.

**Acceptance Scenarios**:

1. **Given** WSL2 and k3s are already available, **When** the developer follows the documented Jenkins start, **Then** they can open Jenkins on a documented local URL and see the eShop CI/CD job.
2. **Given** the job is open, **When** the developer starts a run, **Then** they MUST be able to choose the git branch before CI starts.
3. **Given** Jenkins is stopped, **When** a developer runs the existing isolated test suites, **Then** those suites still pass.

---

### Edge Cases

- Chosen branch exists locally but not on the remote: CI still checks out that local/ref name if git can resolve it; otherwise fail “branch not found”.
- Chosen branch is the branch already deployed: CI still runs; CD may roll the same revision and MUST still leave Catalog Ready.
- k3s is stopped when CD starts: CD fails with a clear “cluster not available” result; CI (if already passed) does not pretend deploy succeeded.
- Jenkins cannot reach git or the working copy: CI fails; CD does not run.
- Catalog image publish or cluster apply fails mid-CD: the run is failed; the operator can see which step failed; the pipeline MUST NOT report success.
- Isolated checks pass but Catalog never becomes Ready: CD fails; create/get-by-id on the documented URL is not claimed successful.
- Two pipeline runs at once: the second run MUST wait or fail clearly; they MUST NOT interleave two different branches onto the same cluster.
- Destructive cluster wipe (delete namespaces / wipe the catalog disk) is **not** part of this CI/CD. Ordinary CD keeps daily catalog data.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The project MUST document how to install and start Jenkins on the same WSL2 Ubuntu that already hosts k3s.
- **FR-002**: The operator MUST be able to start a pipeline run and **choose the git branch** to verify and deploy before CI starts.
- **FR-003**: CI MUST check out the chosen branch and run the existing isolated Catalog and building-block test suites.
- **FR-004**: Isolated CI checks MUST NOT start k3s, MUST NOT apply cluster manifests, and MUST NOT delete cluster namespaces or the catalog disk.
- **FR-005**: CD MUST run only after CI passed for that same chosen branch.
- **FR-006**: CD MUST deploy that branch onto the k3s cluster on **this** machine (the documented local cluster from feature 009), not a cloud cluster and not a second laptop.
- **FR-007**: After a successful CD, Catalog on the documented local HTTP URL MUST serve the chosen branch; create-product and get-by-id MUST succeed while the store is up.
- **FR-008**: Catalog Ready during and after CD MUST still wait only on the catalog store (search and viewer MUST NOT gate Ready).
- **FR-009**: Default CD MUST keep the existing cluster catalog store (no namespace wipe, no catalog-disk wipe).
- **FR-010**: A missing branch, failed isolated checks, stopped k3s, or failed deploy MUST fail the run and MUST NOT report a successful deploy.
- **FR-011**: Existing isolated `dotnet test` suites MUST still pass with Jenkins stopped and with k3s stopped.
- **FR-012**: Compose files MUST stay. This feature MUST NOT require running compose.

### Key Entities

- **Pipeline run**: One operator-started CI then (if CI passed) CD. Attributes: chosen branch, CI result, CD result, start/finish.
- **Chosen branch**: The git branch name the operator selected for this run. CI and CD MUST use the same name.
- **Local cluster target**: The existing k3s cluster on this WSL2 machine (namespaces `catalog`, `search`, `viewer`). CD updates this target in place.
- **Verification result**: Pass/fail of isolated checks. Fail blocks CD.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a WSL2 machine that already has k3s, an operator who follows the run notes can open Jenkins, pick a branch, and finish a passing CI run in under 15 minutes.
- **SC-002**: After a passing CI, CD deploys that same branch and the operator can complete one create-product plus one get-by-id on the documented Catalog URL in under 15 minutes (Catalog Ready may precede search/viewer Ready).
- **SC-003**: 100% of runs where isolated checks fail leave the running cluster Catalog on its previous revision (no successful CD).
- **SC-004**: 100% of runs with a branch name git cannot resolve fail in CI and never start CD.
- **SC-005**: Existing isolated Catalog and building-block test suites still pass with Jenkins stopped and with the local cluster stopped.
- **SC-006**: After ordinary (non-destructive) CD, products that were already in the cluster catalog store are still gettable by id.

## Assumptions

- The developer works in **WSL2** on Windows. Jenkins and k3s run on **the same Ubuntu**. A remote Jenkins controller or a second build agent is out of scope.
- The official cluster path remains **k3s from feature 009**. This feature adds automation on top; it does not replace the documented `kubectl apply` start.
- This is **local development only**. Cloud Jenkins, GitHub-hosted runners, production clusters, and ingress certificates are out of scope.
- **Compose files stay**. Running compose is out of scope.
- Isolated `dotnet test` remains the quality gate for CI and does not start a cluster.
- Default CD is **apply/rollout**, not a destructive wipe. Wiping namespaces to “reinit” stays a manual operator action outside this pipeline.
- Local Jenkins uses the same class of local-dev access as other local UIs (reachable on a documented local URL; no production SSO).
- The operator starts runs **by hand** and picks the branch each time. Automatic deploy of every git push is out of scope.
- Concurrent runs of this job on the same machine are serialized or rejected; two branches must not deploy at the same time.
