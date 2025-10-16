# Local development

The repository targets the .NET 8 SDK. Several online build agents already include the SDK by default, but the execution
environment for these exercises starts from a minimal image without the `dotnet` CLI. Use one of the following approaches to
make the SDK available before running `dotnet` commands.

## Option 1: Install with the official script

```bash
# download the installer and install the desired SDK version under ~/.dotnet
curl -SL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --channel 8.0 --install-dir "$HOME/.dotnet"

# expose the CLI for the current shell
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"

dotnet --info
```

If your environment sits behind a proxy that blocks direct downloads, fetch the script and the SDK archives from a machine with
internet access, copy them into the container, and point `--install-dir` to that location.

## Option 2: Use the .NET SDK container image

```bash
docker run --rm -v "$PWD:/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 dotnet build
```

This approach runs the build inside the official SDK image without installing anything on the host. Ensure that Docker is
available in your environment before using this option.

## Verifying the installation

After either option you should be able to run the usual commands:

```bash
dotnet restore
dotnet build
dotnet test
```

If `dotnet` is still not found, double-check that `$DOTNET_ROOT` is on the `PATH` and that the SDK was extracted to the
expected folder.
