# Non-free KiCad Assets

This project uses non-free content (e.g., vendor-specific footprints,
licensed models, or redistributable-but-not-open assets). This content is expected in the directory structure:
```
hardware/kicad-project/nonfree/
    3dmodels/
        *.stp

    footprints/
        <PROJECTNAME>.pretty
        
    symbols/
        <PROJECTNAME>.kicad_sym
```

The content is stored in a private Git submodule:
  https://github.com/getpwnam/m0dmf_kicad_nonfree

## How to fetch it

If you have access:

    git submodule update --init --recursive

If you do *not* have access, this directory will remain empty.

## Component Search Engine

All of the required symbols are available from https://componentsearchengine.com
You'll just need to fetch them yourself and copy them into the appropriate directories here.
