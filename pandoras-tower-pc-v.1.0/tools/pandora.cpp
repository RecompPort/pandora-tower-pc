// Pandora's Tower (SX3EXJ, SX3P01, SX3J01) — la capa propia del port sobre el runtime de wiikit.
//
// Enlazada en wiiboot por pandora.cmake. Aquí va solo lo que necesita este
// juego. El KPADStatus del SDK de 2010 mide 0xF0 bytes (KPADReadEx multiplica
// por 0xF0 en 805E9814 y 805E98E4), que es el valor por defecto del runtime.
#include "rt.h"
#include <chrono>
#include <cstdlib>
#include <cstring>

// del runtime de wiikit (parches 0015 y 0021): 1 una película empieza, 2 una
// pantalla de carga empieza, 3 la pantalla de carga termina, 0 nada
void video_letterbox_hint(int mode);

namespace {

// ---- el ritmo de dibujo ------------------------------------------------------------------------
// El motor separa la lógica del dibujo. El constructor base del objeto de
// sistema (804C2628) fija +0x434 (pasos de lógica por segundo) y +0x438
// (fotogramas dibujados por segundo) a 60. El bucle principal
// (pt_SystemMainLoop, 804F7EF4) da una vuelta por campo de vídeo: avanza la
// lógica un paso (más 0.2 en PAL de 50 Hz, por +0x47E) y solo dibuja cuando
// su contador de campos es múltiplo de 60 / (+0x438). Lo que se actualiza al
// dibujar (804C2844, 8048C7B4) se escala por +0x434 / +0x438, así que dibujar
// tantas veces como avanza la lógica no cambia la velocidad del juego.
// PANDORA_FPS=60 (./run.sh --60fps) fija +0x438 a 60 en cada vuelta.
// Cada 10 s se informa de las vueltas por segundo, de cuántas querían dibujar
// y de los valores que el juego tiene: así se ve qué hace sin tocar nada.
#ifdef PANDORA_JP
// SX3J01 (la japonesa, la primera): el mismo motor sin el soporte de PAL. El
// objeto de sistema tiene 8 bytes menos antes de estos campos (el bucle lee el
// ritmo en +0x438 y la marca de dibujo en +0x440) y no hay paso fraccionario
// de 50 Hz; el puntero está en otra dirección (r13 - 0x59B0).
constexpr uint32_t kSystem = 0x807EA2D0;
constexpr uint32_t kLogicRate = 0x434, kRenderRate = 0x438, kDrawFlag = 0x440, kStepInc = 0;
#else
// SX3EXJ y SX3P01 (el mismo ejecutable)
constexpr uint32_t kSystem = 0x807F2058;       // puntero al objeto de sistema
constexpr uint32_t kLogicRate = 0x434, kRenderRate = 0x438, kDrawFlag = 0x448, kStepInc = 0x47E;
#endif

PPCFunc orig_main_loop = nullptr;
uint32_t want_fps = 0;

void main_loop(PPCContext& c) {
    const uint32_t sys = c.r[3];
    if (want_fps) st32(sys + kRenderRate, want_fps);
    orig_main_loop(c);

    using Clock = std::chrono::steady_clock;
    static Clock::time_point t0 = Clock::now();
    static uint32_t loops = 0, want_draw = 0;
    static bool first = true;
    ++loops;
    if (ld8(sys + kDrawFlag)) ++want_draw;               // si esta vuelta quería dibujar
    if (first) {
        first = false;
        rt_log("pandora: system %08X (at %08X): logic rate %u, render rate %u, step increment %#x",
               sys, ld32(kSystem), ld32(sys + kLogicRate), ld32(sys + kRenderRate), kStepInc ? (unsigned)ld16(sys + kStepInc) : 0u);
    }
    double s = std::chrono::duration<double>(Clock::now() - t0).count();
    if (s >= 10.0) {
        rt_log("pandora: %.1f loops/s, %.1f wanting to draw; logic rate %u, render rate %u",
               loops / s, want_draw / s, ld32(sys + kLogicRate), ld32(sys + kRenderRate));
        t0 = Clock::now();
        loops = want_draw = 0;
    }
}

// ---- la pantalla de la correa -----------------------------------------------------------------
// Al arrancar, la escena del título enseña la pantalla que pide ponerse la
// correa del Wii Remote: una tarea (pt_StrapTask, 8034BA78) con estados en
// +0xD0 de la escena: 0 aparece (la cortina +0xA4 va de 1 a 0), 1 espera,
// 2 un segundo, 3 espera a que se pulse A (o 20 s), 4 desaparece (la cortina
// de 0 a 1) y libera sus imágenes, 5 diez fotogramas y pasa al título. Para
// saltarla, en su primera vuelta se deja la cortina cerrada (1) y se pasa al
// estado 4: las imágenes se liberan sin llegar a verse. PANDORA_STRAP=1 la deja.
PPCFunc orig_strap = nullptr;

bool guest_ptr(uint32_t a) { return (a >= 0x80000000u && a < 0x81800000u) || (a >= 0x90000000u && a < 0x94000000u); }

void strap_task(PPCContext& c) {
    uint32_t task = c.r[3];
    if (guest_ptr(task)) {
        uint32_t scene = ld32(task);
        scene = guest_ptr(scene) ? ld32(scene + 0x10) : 0;
        if (guest_ptr(scene) && ld32(scene + 0xD0) < 4) {
            uint32_t p = ld32(scene + 0xA4);                 // la cortina: [[[[+0xA4]]+0x74]+0x14]+0x24, valor en +0x1C
            for (uint32_t off : {0u, 0x74u, 0x14u, 0x24u}) p = guest_ptr(p) ? ld32(p + off) : 0;
            if (guest_ptr(p)) {
                st32(p + 0x1C, 0x3F800000u);                // 1.0: cerrada
                st32(scene + 0xD0, 4);
                static bool told = false;
                if (!told) { told = true; rt_log("pandora: wrist-strap screen skipped"); }
            }
        }
    }
    orig_strap(c);
}

// ---- las películas ------------------------------------------------------------------------------
// La intro y las cinemáticas grandes son películas Mobiclip (.mo) con las bandas
// negras dentro de la imagen; las escenas del motor no las llevan en 16:9 (el
// juego apaga su modo letterbox en pantalla ancha). Abrir y cerrar una película
// pasa por MOOpen y MOClose del SDK: se lo dicen al renderizador, que con la
// opción de quitar las bandas solo las busca mientras hay película y mide la
// primera imagen de cada una antes de mostrarla (sin que se note el cambio).
PPCFunc orig_mo_open = nullptr, orig_mo_close = nullptr;

int movie_log = 0;                                     // las primeras, al registro

void mo_open(PPCContext& c) {
    orig_mo_open(c);
    int32_t r = (int32_t)c.r[3];
    if (movie_log < 40) { ++movie_log; rt_log("pandora: movie starts%s", r >= 0 ? "" : " (failed)"); }
    if (r >= 0) video_letterbox_hint(1);
}

void mo_close(PPCContext& c) {
    orig_mo_close(c);
    if (movie_log < 40) { ++movie_log; rt_log("pandora: movie ends"); }
    video_letterbox_hint(0);
}

// ---- las bandas de las escenas -------------------------------------------------------------------
// Las escenas del juego meten bandas negras deslizándolas desde arriba y abajo:
// pt_ScreenFrameIn (800FE238) crea ese efecto de pantalla y devuelve su
// identificador, que quien lo llama guarda y, solo si no es 0, pasa después a
// pt_ScreenFrameOut para quitarlas. Con PANDORA_SIN_BANDAS=1 no se crea: devuelve
// 0, como si no hubiera bandas, y la escena se ve entera a toda la pantalla.
PPCFunc orig_frame_in = nullptr;

void frame_in(PPCContext& c) {
    static bool told = false;
    if (!told) { told = true; rt_log("pandora: cutscene bars left out (the scene is drawn whole)"); }
    c.r[3] = 0;
}

// ---- agitar el mando ----------------------------------------------------------------------------
// El juego da por agitado el Wii Remote (o el Nunchuk) cuando su acc_value llega a
// un umbral (3 g y 1,85 g por defecto): pt_InputShakeUpdate guarda cada uno en
// +0x254 y +0x258 y cuenta en +0x25C y +0x260 los fotogramas por encima; con 1,
// una sacudida. Aquí solo se anota en el registro, para ver que llegan.
#ifdef PANDORA_JP
constexpr uint32_t kShakeThreshold = 0x807EFC80 - 0x5E50;   // r13 - 0x5E50
#else
constexpr uint32_t kShakeThreshold = 0x807F79C0 - 0x5E08;   // r13 - 0x5E08
#endif
PPCFunc orig_shake_update = nullptr;

float ldf(uint32_t a) { uint32_t u = ld32(a); float f; std::memcpy(&f, &u, 4); return f; }

void shake_update(PPCContext& c) {
    const uint32_t self = c.r[3];
    orig_shake_update(c);
    static int told = 0;
    if (told >= 12 || !guest_ptr(self)) return;
    if (ld32(self + 0x25C) == 1) {
        ++told;
        rt_log("pandora: the game counted a shake of the Remote (%.1f g; it needs %.1f g)", ldf(self + 0x254), ldf(kShakeThreshold));
    }
    if (ld32(self + 0x260) == 1) {
        ++told;
        rt_log("pandora: the game counted a shake of the Nunchuk (%.1f g; it needs %.1f g)", ldf(self + 0x258), ldf(kShakeThreshold + 4));
    }
}

// ---- las pantallas de carga ----------------------------------------------------------------------
// Entre zonas, el guion abre la pantalla de carga (humo con el emblema) con
// NowLoadingStart y la cierra con NowLoadingEnd; trae sus propias bandas negras.
// Se lo dicen al renderizador, que las recorta desde su primera imagen (con
// las medidas de la anterior) y hasta que desaparecen.
PPCFunc orig_loading_start = nullptr, orig_loading_end = nullptr;

void loading_start(PPCContext& c) {
    orig_loading_start(c);
    video_letterbox_hint(2);
}

void loading_end(PPCContext& c) {
    orig_loading_end(c);
    video_letterbox_hint(3);
}

void install() {
    // ---- los controles
    // El juego admite dos esquemas: Wii Remote + Nunchuk (el Remote apunta la
    // cadena) y Classic Controller (la cadena se apunta con el stick derecho).
    // El runtime lleva los mandos de PC (XInput, DualShock, DualSense...) al
    // Classic Controller, y el ratón y el teclado al Remote + Nunchuk, así que
    // el esquema se elige al arrancar: PANDORA_INPUT=remote (./run.sh --raton)
    // o, por defecto, el Classic Controller (mando, y teclado como respaldo).
    const char* m = std::getenv("PANDORA_INPUT");
    bool remote = m && std::strcmp(m, "remote") == 0;
    if (remote) wpad_set_nunchuk(true);
    else wpad_set_classic(true);
#ifdef PANDORA_FIXES
    rt_log("pandora: built with %d port fixes (tools/patches)", PANDORA_FIXES);
#endif
    rt_log("pandora: controls %s", remote ? "Wii Remote + Nunchuk (mouse and keyboard)"
                                          : "Classic Controller (gamepad, keyboard as backup)");

    // ---- el ritmo de dibujo
    const char* f = std::getenv("PANDORA_FPS");
    want_fps = f ? (uint32_t)std::atoi(f) : 0;
    if (want_fps != 0 && want_fps != 30 && want_fps != 60) {
        rt_log("pandora: PANDORA_FPS=%s ignored (30 or 60)", f);
        want_fps = 0;
    }
    // ---- la pantalla de la correa
    const char* strap = std::getenv("PANDORA_STRAP");
    if (!(strap && strap[0] == '1')) {
        orig_strap = ppc_hook("pt_StrapTask", strap_task);
        if (!orig_strap) rt_log("pandora: pt_StrapTask has no hook slot: run ./setup.sh again");
    }

    if (const char* nb = std::getenv("PANDORA_SIN_BANDAS"); nb && nb[0] == '1') {
        orig_frame_in = ppc_hook("pt_ScreenFrameIn", frame_in);
        if (!orig_frame_in) rt_log("pandora: pt_ScreenFrameIn has no hook slot: run ./setup.sh again");
    }

    orig_shake_update = ppc_hook("pt_InputShakeUpdate", shake_update);
    if (!orig_shake_update) rt_log("pandora: pt_InputShakeUpdate has no hook slot: run ./setup.sh again");

    orig_loading_start = ppc_hook("pt_NowLoadingStart", loading_start);
    orig_loading_end = ppc_hook("pt_NowLoadingEnd", loading_end);
    if (!orig_loading_start || !orig_loading_end) rt_log("pandora: pt_NowLoadingStart/End have no hook slot: run ./setup.sh again");

    orig_mo_open = ppc_hook("MOOpen", mo_open);
    orig_mo_close = ppc_hook("MOClose", mo_close);
    if (!orig_mo_open || !orig_mo_close) rt_log("pandora: MOOpen/MOClose have no hook slot: run ./setup.sh again");
    else {
        rt_log("pandora: movies tracked (MOOpen, MOClose)");
        // las bandas de las escenas no se crean (PANDORA_SIN_BANDAS): el renderizador
        // solo las busca en las películas, y una escena oscura no pasa por bandas
        if (orig_frame_in) video_letterbox_hint(0);
    }

    orig_main_loop = ppc_hook("pt_SystemMainLoop", main_loop);
    if (!orig_main_loop) rt_log("pandora: pt_SystemMainLoop has no hook slot: run ./setup.sh again");
    else if (want_fps) rt_log("pandora: drawing at %u fps (experimental)", want_fps);
}

RtGameLayer layer("Pandora's Tower", install);

}  // namespace
