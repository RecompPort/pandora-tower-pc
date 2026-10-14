# La capa propia del port, incluida por la compilación generada con
# -DWIIKIT_EXTRA=<este archivo>: se enlaza dentro de wiiboot.
target_sources(wiiboot PRIVATE ${CMAKE_CURRENT_LIST_DIR}/pandora.cpp)
# -DPANDORA_JP=ON (setup.sh, con la versión japonesa): sus direcciones
if(PANDORA_JP)
    target_compile_definitions(wiiboot PRIVATE PANDORA_JP=1)
endif()
# cuántos arreglos de tools/patches había al compilar: run.err lo dice al arrancar
# (solo para pandora.cpp, para no recompilar nada más cuando cambia)
file(GLOB _pandora_fixes ${CMAKE_CURRENT_LIST_DIR}/patches/*.patch)
list(LENGTH _pandora_fixes _pandora_nfixes)
set_property(SOURCE ${CMAKE_CURRENT_LIST_DIR}/pandora.cpp APPEND PROPERTY COMPILE_DEFINITIONS PANDORA_FIXES=${_pandora_nfixes})
