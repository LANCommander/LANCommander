These are the standard libyaml sources from https://github.com/yaml/libyaml (MIT
license), version 0.2.5. See License.

Copied here: `include/yaml.h`, `src/yaml_private.h` and the eight `src/*.c`
files, flattened into this directory. Nothing is patched.

libyaml normally gets its version macros from an autotools-generated
`config.h`. We do not run autotools, so `HAVE_CONFIG_H` stays undefined and the
four `YAML_VERSION_*` macros are supplied on the compile line instead — see the
`yaml` target in `liblancommander/CMakeLists.txt`.

`setup-vendor.ps1` at the repository root re-downloads these files.
