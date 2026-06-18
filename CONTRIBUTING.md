# Contributing

## Branch & PR workflow

- Work on a feature branch, open a Pull Request against `main`, merge after CI passes.
- CI (`.github/workflows/ci.yml`) runs build, TruffleHog secret scan and the CycloneDX SBOM
  on every PR and on pushes to `main`.

## Signed commits

All commits to this repository are **signed** so GitHub shows the **Verified** badge. We use
SSH commit signing (simpler than GPG on macOS/Linux, no agent setup needed).

### One-time setup per developer

1. **Create a signing key** (dedicated, separate from any auth key):
   ```bash
   ssh-keygen -t ed25519 -f ~/.ssh/id_ed25519_signing -C "<you>-shellyspotter-signing"
   ```

2. **Configure git for this repo** (local config — does not affect your other repos):
   ```bash
   cd shellyspotter
   git config --local gpg.format ssh
   git config --local user.signingkey ~/.ssh/id_ed25519_signing.pub
   git config --local commit.gpgsign true
   git config --local tag.gpgsign true
   # Use your GitHub no-reply e-mail so commits link to your account and verify:
   git config --local user.email "<id>+<user>@users.noreply.github.com"
   ```
   (Find your no-reply address under GitHub → Settings → Emails.)

3. **Register the key on GitHub** so it verifies:
   - Copy the public key: `cat ~/.ssh/id_ed25519_signing.pub`
   - GitHub → Settings → **SSH and GPG keys** → **New SSH key**
   - **Key type: `Signing Key`** (not "Authentication Key"), paste, save.

4. *(Optional)* enable local verification of others' commits:
   ```bash
   echo "<your-noreply-email> $(cat ~/.ssh/id_ed25519_signing.pub)" >> ~/.ssh/allowed_signers
   git config --local gpg.ssh.allowedSignersFile ~/.ssh/allowed_signers
   ```

### Verifying

```bash
git log --show-signature -1     # local check
git verify-commit HEAD          # exits 0 if the signature is valid
```

On GitHub, signed commits from a registered key show a green **Verified** badge.
