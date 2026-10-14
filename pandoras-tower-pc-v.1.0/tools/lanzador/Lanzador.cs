// Lanzador de Pandora's Tower (port nativo con wiikit).
//
// Ventana propia, dibujada a mano. A la izquierda, una columna con el logo y
// tres páginas de opciones (Gráficos, Controles, Juego) y el botón Jugar; a la
// derecha, el arte. En Controles se asignan los botones del mando y las teclas
// y el ratón: se guardan en controles.txt, el archivo de teclas de wiiboot,
// que el lanzador le pasa con --keys. Las demás opciones van a lanzador.ini.
// Lo que informa el juego queda en build/run.err.
//
// C# 5 a propósito: lanzador.sh lo compila con el csc.exe que trae Windows
// (.NET Framework 4), sin instalar nada. Si no lo hay, Lanzador.ps1 lo carga
// con Add-Type.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace PandoraLauncher
{
    // The launcher's own language: the "idioma" option (the game's too). Every
    // text is written in Spanish in the code and shown through T().
    static class L
    {
        public static string Lang = "es";
        static readonly string[] Order = { "en", "de", "it", "fr", "ja", "zh" };
        static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
        {
            { "Gráficos", new[] { "Graphics", "Grafik", "Grafica", "Graphismes", "グラフィック", "图形" } },
            { "Texturas", new[] { "Textures", "Texturen", "Texture", "Textures", "テクスチャ", "纹理" } },
            { "Controles", new[] { "Controls", "Steuerung", "Controlli", "Commandes", "操作", "控制" } },
            { "Juego", new[] { "Game", "Spiel", "Gioco", "Jeu", "ゲーム", "游戏" } },
            { "Resolución", new[] { "Resolution", "Auflösung", "Risoluzione", "Résolution", "解像度", "分辨率" } },
            { "Nativa de Wii · 640 × 528", new[] { "Wii native · 640 × 528", "Wii-nativ · 640 × 528", "Nativa Wii · 640 × 528", "Native Wii · 640 × 528", "Wii本来 · 640 × 528", "Wii原生 · 640 × 528" } },
            { "Nativa de tu pantalla · x{0} · {1} × {2}", new[] { "Your screen's · x{0} · {1} × {2}", "Bildschirm-nativ · x{0} · {1} × {2}", "Nativa del tuo schermo · x{0} · {1} × {2}", "Native de ton écran · x{0} · {1} × {2}", "画面に合わせる · x{0} · {1} × {2}", "屏幕原生 · x{0} · {1} × {2}" } },
            { "  (recomendada)", new[] { "  (recommended)", "  (empfohlen)", "  (consigliata)", "  (recommandée)", "（推奨）", "（推荐）" } },
            { "Hay arreglos nuevos sin compilar: ejecuta ./setup.sh en MSYS2 CLANG64.", new[] { "There are new fixes not built yet: run ./setup.sh in MSYS2 CLANG64.", "Neue Korrekturen noch nicht kompiliert: führe ./setup.sh in MSYS2 CLANG64 aus.", "Ci sono nuove correzioni da compilare: esegui ./setup.sh in MSYS2 CLANG64.", "De nouveaux correctifs ne sont pas compilés : lance ./setup.sh dans MSYS2 CLANG64.", "未ビルドの修正があります：MSYS2 CLANG64 で ./setup.sh を実行してください。", "有尚未编译的新修复：请在 MSYS2 CLANG64 中运行 ./setup.sh。" } },
            { "Cruceta", new[] { "D-pad", "Steuerkreuz", "Croce", "Croix", "十字キー", "十字键" } },
            { "Fotogramas", new[] { "Frame rate", "Bildrate", "Fotogrammi", "Images par seconde", "フレームレート", "帧率" } },
            { "30 fps (original)", new[] { "30 fps (original)", "30 fps (Original)", "30 fps (originale)", "30 fps (d'origine)", "30 fps（オリジナル）", "30 fps（原版）" } },
            { "60 fps (interpolado)", new[] { "60 fps (interpolated)", "60 fps (interpoliert)", "60 fps (interpolati)", "60 fps (interpolées)", "60 fps（補間）", "60 fps（插帧）" } },
            { "60 fps nativos (capa rígida)", new[] { "60 fps native (stiff cape)", "60 fps nativ (starrer Umhang)", "60 fps nativi (mantello rigido)", "60 fps natifs (cape rigide)", "ネイティブ60 fps（マントが硬直）", "原生60 fps（披风僵直）" } },
            { "90 fps (interpolado)", new[] { "90 fps (interpolated)", "90 fps (interpoliert)", "90 fps (interpolati)", "90 fps (interpolées)", "90 fps（補間）", "90 fps（插帧）" } },
            { "120 fps (interpolado)", new[] { "120 fps (interpolated)", "120 fps (interpoliert)", "120 fps (interpolati)", "120 fps (interpolées)", "120 fps（補間）", "120 fps（插帧）" } },
            { "Al ritmo de tu monitor (interpolado)", new[] { "Match your monitor (interpolated)", "Monitorfrequenz (interpoliert)", "Frequenza del monitor (interpolati)", "Fréquence de l'écran (interpolées)", "モニターに合わせる（補間）", "跟随显示器（插帧）" } },
            { "Ilimitado (interpolado)", new[] { "Unlimited (interpolated)", "Unbegrenzt (interpoliert)", "Illimitati (interpolati)", "Illimitées (interpolées)", "無制限（補間）", "无限制（插帧）" } },
            { "Pantalla", new[] { "Display", "Anzeige", "Schermo", "Affichage", "画面", "显示" } },
            { "Completa sin bordes", new[] { "Borderless fullscreen", "Randloses Vollbild", "Schermo intero senza bordi", "Plein écran sans bordure", "ボーダーレス全画面", "无边框全屏" } },
            { "Completa exclusiva", new[] { "Exclusive fullscreen", "Exklusives Vollbild", "Schermo intero esclusivo", "Plein écran exclusif", "排他的全画面", "独占全屏" } },
            { "En ventana", new[] { "Windowed", "Fenster", "In finestra", "Fenêtré", "ウィンドウ", "窗口" } },
            { "Bandas negras", new[] { "Black bars", "Schwarze Balken", "Bande nere", "Bandes noires", "黒帯", "黑边" } },
            { "Como en la consola", new[] { "As on the console", "Wie auf der Konsole", "Come sulla console", "Comme sur la console", "本体と同じ", "与主机相同" } },
            { "Quitar: llenar la pantalla", new[] { "Remove: fill the screen", "Entfernen: Bild füllen", "Rimuovi: riempi lo schermo", "Retirer : remplir l'écran", "除去：画面いっぱい", "去除：填满屏幕" } },
            { "Quitar: estirar la imagen", new[] { "Remove: stretch the picture", "Entfernen: Bild strecken", "Rimuovi: allunga l'immagine", "Retirer : étirer l'image", "除去：引き伸ばす", "去除：拉伸画面" } },
            { "Quitar todas: 16:9 completo", new[] { "Remove all: full 16:9", "Alle entfernen: volles 16:9", "Rimuovi tutte: 16:9 pieno", "Tout retirer : 16:9 complet", "すべて除去：完全な16:9", "全部去除：完整16:9" } },
            { "Filtrado de texturas", new[] { "Texture filtering", "Texturfilterung", "Filtro texture", "Filtrage des textures", "テクスチャフィルタ", "纹理过滤" } },
            { "x16 (el más nítido)", new[] { "x16 (sharpest)", "x16 (am schärfsten)", "x16 (il più nitido)", "x16 (le plus net)", "x16（最も鮮明）", "x16（最清晰）" } },
            { "Original (sin filtrado)", new[] { "Original (no filtering)", "Original (ohne Filter)", "Originale (senza filtro)", "D'origine (sans filtrage)", "オリジナル（フィルタなし）", "原版（无过滤）" } },
            { "Paquete de texturas", new[] { "Texture pack", "Texturpaket", "Pacchetto texture", "Pack de textures", "テクスチャパック", "纹理包" } },
            { "Ninguno (las originales)", new[] { "None (the originals)", "Keins (die originalen)", "Nessuno (le originali)", "Aucun (celles d'origine)", "なし（オリジナル）", "无（原版）" } },
            { "Esquema de control", new[] { "Control scheme", "Steuerungsschema", "Schema di controllo", "Schéma de commande", "操作方法", "控制方案" } },
            { "Mando (Classic Controller)", new[] { "Gamepad (Classic Controller)", "Gamepad (Classic Controller)", "Controller (Classic Controller)", "Manette (Classic Controller)", "コントローラー（クラシック）", "手柄（传统手柄）" } },
            { "Ratón y teclado (Wii Remote + Nunchuk)", new[] { "Mouse and keyboard (Wii Remote + Nunchuk)", "Maus und Tastatur (Wii Remote + Nunchuk)", "Mouse e tastiera (Wii Remote + Nunchuk)", "Souris et clavier (Wiimote + Nunchuk)", "マウスとキーボード（リモコン＋ヌンチャク）", "鼠标和键盘（遥控器＋双节棍）" } },
            { "Iconos de los botones", new[] { "Button icons", "Tastensymbole", "Icone dei tasti", "Icônes des boutons", "ボタンアイコン", "按键图标" } },
            { "Originales (Wii)", new[] { "Original (Wii)", "Original (Wii)", "Originali (Wii)", "D'origine (Wii)", "オリジナル（Wii）", "原版（Wii）" } },
            { "Teclado y ratón", new[] { "Keyboard and mouse", "Tastatur und Maus", "Tastiera e mouse", "Clavier et souris", "キーボードとマウス", "键盘和鼠标" } },
            { "Agitar el Wii Remote", new[] { "Shake the Wii Remote", "Wii Remote schütteln", "Agitare il Wii Remote", "Secouer la Wiimote", "リモコンを振る", "挥动遥控器" } },
            { "Tecla y ratón agitado", new[] { "Key and shaken mouse", "Taste und Mausschütteln", "Tasto e mouse agitato", "Touche et souris secouée", "キーとマウスを振る", "按键和晃动鼠标" } },
            { "Solo su tecla", new[] { "Its key only", "Nur ihre Taste", "Solo il suo tasto", "Sa touche seulement", "キーのみ", "仅按键" } },
            { "Zona muerta de los sticks", new[] { "Stick dead zone", "Stick-Totzone", "Zona morta degli stick", "Zone morte des sticks", "スティックのデッドゾーン", "摇杆死区" } },
            { "Idioma", new[] { "Language", "Sprache", "Lingua", "Langue", "言語", "语言" } },
            { "Pantalla de la correa", new[] { "Wrist strap screen", "Handschlaufen-Hinweis", "Schermata del laccetto", "Écran de la dragonne", "ストラップ注意画面", "腕带提示画面" } },
            { "Omitir al arrancar", new[] { "Skip at startup", "Beim Start überspringen", "Salta all'avvio", "Passer au démarrage", "起動時にスキップ", "启动时跳过" } },
            { "Mostrar, como en la Wii", new[] { "Show, as on the Wii", "Anzeigen, wie auf der Wii", "Mostra, come su Wii", "Afficher, comme sur Wii", "表示（Wiiと同じ）", "显示（与Wii相同）" } },
            { "Cruceta ↑", new[] { "D-pad ↑", "Steuerkreuz ↑", "Croce ↑", "Croix ↑", "十字ボタン↑", "十字键↑" } },
            { "Cruceta ↓", new[] { "D-pad ↓", "Steuerkreuz ↓", "Croce ↓", "Croix ↓", "十字ボタン↓", "十字键↓" } },
            { "Cruceta ←", new[] { "D-pad ←", "Steuerkreuz ←", "Croce ←", "Croix ←", "十字ボタン←", "十字键←" } },
            { "Cruceta →", new[] { "D-pad →", "Steuerkreuz →", "Croce →", "Croix →", "十字ボタン→", "十字键→" } },
            { "Stick izq. ↑", new[] { "Left stick ↑", "Linker Stick ↑", "Stick sin. ↑", "Stick gauche ↑", "左スティック↑", "左摇杆↑" } },
            { "Stick izq. ↓", new[] { "Left stick ↓", "Linker Stick ↓", "Stick sin. ↓", "Stick gauche ↓", "左スティック↓", "左摇杆↓" } },
            { "Stick izq. ←", new[] { "Left stick ←", "Linker Stick ←", "Stick sin. ←", "Stick gauche ←", "左スティック←", "左摇杆←" } },
            { "Stick izq. →", new[] { "Left stick →", "Linker Stick →", "Stick sin. →", "Stick gauche →", "左スティック→", "左摇杆→" } },
            { "Stick der. ↑", new[] { "Right stick ↑", "Rechter Stick ↑", "Stick des. ↑", "Stick droit ↑", "右スティック↑", "右摇杆↑" } },
            { "Stick der. ↓", new[] { "Right stick ↓", "Rechter Stick ↓", "Stick des. ↓", "Stick droit ↓", "右スティック↓", "右摇杆↓" } },
            { "Stick der. ←", new[] { "Right stick ←", "Rechter Stick ←", "Stick des. ←", "Stick droit ←", "右スティック←", "右摇杆←" } },
            { "Stick der. →", new[] { "Right stick →", "Rechter Stick →", "Stick des. →", "Stick droit →", "右スティック→", "右摇杆→" } },
            { "Stick ↑", new[] { "Stick ↑", "Stick ↑", "Stick ↑", "Stick ↑", "スティック↑", "摇杆↑" } },
            { "Stick ↓", new[] { "Stick ↓", "Stick ↓", "Stick ↓", "Stick ↓", "スティック↓", "摇杆↓" } },
            { "Stick ←", new[] { "Stick ←", "Stick ←", "Stick ←", "Stick ←", "スティック←", "摇杆←" } },
            { "Stick →", new[] { "Stick →", "Stick →", "Stick →", "Stick →", "スティック→", "摇杆→" } },
            { "Agitar", new[] { "Shake", "Schütteln", "Agita", "Secouer", "振る", "摇动" } },
            { "Agitar el Nunchuk", new[] { "Shake the Nunchuk", "Nunchuk schütteln", "Agita il Nunchuk", "Secouer le Nunchuk", "ヌンチャクを振る", "摇动双节棍" } },
            { "Nunchuk C", new[] { "Nunchuk C", "Nunchuk C", "Nunchuk C", "Nunchuk C", "ヌンチャクC", "双节棍C" } },
            { "Nunchuk Z", new[] { "Nunchuk Z", "Nunchuk Z", "Nunchuk Z", "Nunchuk Z", "ヌンチャクZ", "双节棍Z" } },
            { "Caminar", new[] { "Walk", "Gehen", "Cammina", "Marcher", "歩く", "行走" } },
            { "A  (abajo)", new[] { "A  (bottom)", "A  (unten)", "A  (in basso)", "A  (bas)", "A（下）", "A（下）" } },
            { "B  (derecha)", new[] { "B  (right)", "B  (rechts)", "B  (destra)", "B  (droite)", "B（右）", "B（右）" } },
            { "X  (izquierda)", new[] { "X  (left)", "X  (links)", "X  (sinistra)", "X  (gauche)", "X（左）", "X（左）" } },
            { "Y  (arriba)", new[] { "Y  (top)", "Y  (oben)", "Y  (in alto)", "Y  (haut)", "Y（上）", "Y（上）" } },
            { "Start / Menú", new[] { "Start / Menu", "Start / Menü", "Start / Menu", "Start / Menu", "スタート／メニュー", "开始／菜单" } },
            { "Back / Vista", new[] { "Back / View", "Zurück / Ansicht", "Back / Visualizza", "Retour / Affichage", "戻る／ビュー", "返回／视图" } },
            { "Botón central", new[] { "Guide button", "Guide-Taste", "Tasto centrale", "Bouton central", "ガイドボタン", "指南键" } },
            { "L3 (stick izq.)", new[] { "L3 (left stick)", "L3 (linker Stick)", "L3 (stick sin.)", "L3 (stick gauche)", "L3（左スティック）", "L3（左摇杆）" } },
            { "R3 (stick der.)", new[] { "R3 (right stick)", "R3 (rechter Stick)", "R3 (stick des.)", "R3 (stick droit)", "R3（右スティック）", "R3（右摇杆）" } },
            { "Intro", new[] { "Enter", "Eingabe", "Invio", "Entrée", "Enter", "回车" } },
            { "Intro (numérico)", new[] { "Enter (keypad)", "Eingabe (Ziffernblock)", "Invio (tastierino)", "Entrée (pavé num.)", "Enter（テンキー）", "回车（小键盘）" } },
            { "Espacio", new[] { "Space", "Leertaste", "Spazio", "Espace", "スペース", "空格" } },
            { "Retroceso", new[] { "Backspace", "Rücktaste", "Backspace", "Retour arrière", "BackSpace", "退格" } },
            { "Mayús izq.", new[] { "Left Shift", "Umschalt links", "Maiusc sin.", "Maj gauche", "左Shift", "左Shift" } },
            { "Mayús der.", new[] { "Right Shift", "Umschalt rechts", "Maiusc des.", "Maj droite", "右Shift", "右Shift" } },
            { "Ctrl izq.", new[] { "Left Ctrl", "Strg links", "Ctrl sin.", "Ctrl gauche", "左Ctrl", "左Ctrl" } },
            { "Ctrl der.", new[] { "Right Ctrl", "Strg rechts", "Ctrl des.", "Ctrl droite", "右Ctrl", "右Ctrl" } },
            { "Ratón izq.", new[] { "Left click", "Linksklick", "Clic sinistro", "Clic gauche", "左クリック", "左键" } },
            { "Ratón der.", new[] { "Right click", "Rechtsklick", "Clic destro", "Clic droit", "右クリック", "右键" } },
            { "Ratón central", new[] { "Middle click", "Mittelklick", "Clic centrale", "Clic du milieu", "中クリック", "中键" } },
            { "Ratón X1", new[] { "Mouse X1", "Maus X1", "Mouse X1", "Souris X1", "マウスX1", "鼠标X1" } },
            { "Ratón X2", new[] { "Mouse X2", "Maus X2", "Mouse X2", "Souris X2", "マウスX2", "鼠标X2" } },
            { "Retro", new[] { "Back", "Rück", "Back", "Retour", "BS", "退格" } },
            { "Mayús", new[] { "Shift", "Umsch", "Maiusc", "Maj", "Shift", "Shift" } },
            { "Ctrl", new[] { "Ctrl", "Strg", "Ctrl", "Ctrl", "Ctrl", "Ctrl" } },
            { "Asignar botones y teclas", new[] { "Assign buttons and keys", "Tasten zuweisen", "Assegna tasti", "Attribuer boutons et touches", "ボタンとキーの割り当て", "分配按键" } },
            { "Elegir la imagen del juego", new[] { "Choose the game image", "Spielabbild wählen", "Scegli l'immagine del gioco", "Choisir l'image du jeu", "ゲームイメージを選択", "选择游戏镜像" } },
            { "Copiar partidas", new[] { "Back up saves", "Spielstände sichern", "Copia salvataggi", "Copier sauvegardes", "セーブをコピー", "备份存档" } },
            { "Abrir partidas", new[] { "Open saves", "Spielstände öffnen", "Apri salvataggi", "Ouvrir sauvegardes", "セーブを開く", "打开存档" } },
            { "Restaurar por defecto", new[] { "Restore defaults", "Standard wiederherstellen", "Ripristina predefiniti", "Rétablir par défaut", "初期設定に戻す", "恢复默认" } },
            { "Hecho", new[] { "Done", "Fertig", "Fatto", "Terminé", "完了", "完成" } },
            { "Jugar", new[] { "Play", "Spielen", "Gioca", "Jouer", "プレイ", "开始游戏" } },
            { "Salir", new[] { "Quit", "Beenden", "Esci", "Quitter", "終了", "退出" } },
            { "Imagen del juego", new[] { "Game image", "Spielabbild", "Immagine del gioco", "Image du jeu", "ゲームイメージ", "游戏镜像" } },
            { "  (carpeta)", new[] { "  (folder)", "  (Ordner)", "  (cartella)", "  (dossier)", "（フォルダ）", "（文件夹）" } },
            { "sin elegir: elige la imagen del juego", new[] { "not chosen: choose the game image", "nicht gewählt: Spielabbild wählen", "non scelta: scegli l'immagine del gioco", "aucune : choisis l'image du jeu", "未選択：ゲームイメージを選んでください", "未选择：请选择游戏镜像" } },
            { "Clic: asignar · clic derecho: borrar (en el mando, cambiar) · rueda o ↑↓: más filas · Esc: volver", new[] { "Click: assign · right click: clear (gamepad: cycle) · wheel or ↑↓: more rows · Esc: back", "Klick: zuweisen · Rechtsklick: löschen (Gamepad: wechseln) · Mausrad oder ↑↓: mehr Zeilen · Esc: zurück", "Clic: assegna · clic destro: cancella (controller: cambia) · rotella o ↑↓: altre righe · Esc: indietro", "Clic : attribuer · clic droit : effacer (manette : changer) · molette ou ↑↓ : plus de lignes · Échap : retour", "クリック：割り当て · 右クリック：消去（コントローラーは切替）· ホイールか↑↓：他の行 · Esc：戻る", "单击：分配 · 右键：清除（手柄：切换）· 滚轮或↑↓：更多行 · Esc：返回" } },
            { "↑↓ elegir · ←→ cambiar · Esc: salir", new[] { "↑↓ select · ←→ change · Esc: quit", "↑↓ wählen · ←→ ändern · Esc: beenden", "↑↓ scegli · ←→ cambia · Esc: esci", "↑↓ choisir · ←→ changer · Échap : quitter", "↑↓ 選択 · ←→ 変更 · Esc：終了", "↑↓ 选择 · ←→ 更改 · Esc：退出" } },
            { "Pulsa un botón del mando…", new[] { "Press a gamepad button…", "Gamepad-Taste drücken…", "Premi un tasto del controller…", "Appuie sur un bouton de la manette…", "コントローラーのボタンを押す…", "请按手柄按键…" } },
            { "Pulsa una tecla o botón del ratón…", new[] { "Press a key or mouse button…", "Taste oder Maustaste drücken…", "Premi un tasto o pulsante del mouse…", "Appuie sur une touche ou un bouton de souris…", "キーかマウスボタンを押す…", "请按键盘键或鼠标键…" } },
            { "Botones · Mando (Classic Controller)", new[] { "Buttons · Gamepad (Classic Controller)", "Tasten · Gamepad (Classic Controller)", "Tasti · Controller (Classic Controller)", "Boutons · Manette (Classic Controller)", "ボタン · コントローラー（クラシック）", "按键 · 手柄（传统手柄）" } },
            { "Teclas · Ratón y teclado (Wii Remote + Nunchuk)", new[] { "Keys · Mouse and keyboard (Wii Remote + Nunchuk)", "Tasten · Maus und Tastatur (Wii Remote + Nunchuk)", "Tasti · Mouse e tastiera (Wii Remote + Nunchuk)", "Touches · Souris et clavier (Wiimote + Nunchuk)", "キー · マウスとキーボード（リモコン＋ヌンチャク）", "按键 · 鼠标和键盘（遥控器＋双节棍）" } },
            { "Botón del juego", new[] { "Game button", "Spieltaste", "Tasto del gioco", "Bouton du jeu", "ゲームのボタン", "游戏按键" } },
            { "Teclado y ratón (respaldo)", new[] { "Keyboard and mouse (backup)", "Tastatur und Maus (Reserve)", "Tastiera e mouse (riserva)", "Clavier et souris (secours)", "キーボードとマウス（予備）", "键盘和鼠标（备用）" } },
            { "Teclado y ratón (el ratón es el puntero)", new[] { "Keyboard and mouse (the mouse is the pointer)", "Tastatur und Maus (die Maus ist der Zeiger)", "Tastiera e mouse (il mouse è il puntatore)", "Clavier et souris (la souris est le pointeur)", "キーボードとマウス（マウスがポインター）", "键盘和鼠标（鼠标为指针）" } },
            { "Mando", new[] { "Gamepad", "Gamepad", "Controller", "Manette", "コントローラー", "手柄" } },
            { "Esa tecla está reservada o no se puede usar.", new[] { "That key is reserved or can't be used.", "Diese Taste ist reserviert oder nicht nutzbar.", "Quel tasto è riservato o non utilizzabile.", "Cette touche est réservée ou inutilisable.", "そのキーは使用できません。", "该键已被保留或无法使用。" } },
            { "Todavía no hay partidas guardadas.", new[] { "There are no saves yet.", "Noch keine Spielstände.", "Non ci sono ancora salvataggi.", "Pas encore de sauvegardes.", "まだセーブデータがありません。", "还没有存档。" } },
            { "Pulsa un botón del mando (Esc: cancelar). Si tu mando no es XInput, usa el clic derecho para cambiarlo.", new[] { "Press a gamepad button (Esc: cancel). If your gamepad isn't XInput, right-click to cycle it.", "Drücke eine Gamepad-Taste (Esc: abbrechen). Ist dein Gamepad kein XInput, wechsle per Rechtsklick.", "Premi un tasto del controller (Esc: annulla). Se non è XInput, usa il clic destro per cambiarlo.", "Appuie sur un bouton (Échap : annuler). Si ta manette n'est pas XInput, fais un clic droit pour changer.", "コントローラーのボタンを押してください（Esc：キャンセル）。XInput以外は右クリックで切り替え。", "请按手柄按键（Esc：取消）。若不是XInput手柄，请用右键切换。" } },
            { "Pulsa una tecla o un botón del ratón (Esc: cancelar, Supr: dejar sin tecla).", new[] { "Press a key or mouse button (Esc: cancel, Del: no key).", "Drücke eine Taste oder Maustaste (Esc: abbrechen, Entf: keine Taste).", "Premi un tasto o un pulsante del mouse (Esc: annulla, Canc: nessun tasto).", "Appuie sur une touche ou un bouton de souris (Échap : annuler, Suppr : aucune).", "キーかマウスボタンを押してください（Esc：キャンセル、Del：なし）。", "请按键盘键或鼠标键（Esc：取消，Del：不分配）。" } },
            { "Controles restaurados.", new[] { "Controls restored.", "Steuerung zurückgesetzt.", "Controlli ripristinati.", "Commandes rétablies.", "操作を初期設定に戻しました。", "已恢复默认控制。" } },
            { "Copia guardada en Documentos: ", new[] { "Backup saved in Documents: ", "In Dokumente gesichert: ", "Copia salvata in Documenti: ", "Copie enregistrée dans Documents : ", "ドキュメントに保存しました：", "已备份到“文档”：" } },
            { "No se pudo hacer la copia.", new[] { "The backup failed.", "Sicherung fehlgeschlagen.", "Copia non riuscita.", "La copie a échoué.", "コピーに失敗しました。", "备份失败。" } },
            { "No se pudo hacer la copia: ", new[] { "The backup failed: ", "Sicherung fehlgeschlagen: ", "Copia non riuscita: ", "La copie a échoué : ", "コピーに失敗しました：", "备份失败：" } },
            { "No se pudieron guardar los controles: ", new[] { "Couldn't save the controls: ", "Steuerung konnte nicht gespeichert werden: ", "Impossibile salvare i controlli: ", "Impossible d'enregistrer les commandes : ", "操作設定を保存できませんでした：", "无法保存控制设置：" } },
            { "No encuentro el juego compilado: ejecuta ./setup.sh y ./lanzador.sh.", new[] { "The compiled game wasn't found: run ./setup.sh and ./lanzador.sh.", "Das kompilierte Spiel fehlt: führe ./setup.sh und ./lanzador.sh aus.", "Gioco compilato non trovato: esegui ./setup.sh e ./lanzador.sh.", "Jeu compilé introuvable : lance ./setup.sh et ./lanzador.sh.", "コンパイル済みのゲームがありません：./setup.sh と ./lanzador.sh を実行してください。", "找不到已编译的游戏：请运行 ./setup.sh 和 ./lanzador.sh。" } },
            { "Elige la imagen del juego en la página Juego.", new[] { "Choose the game image on the Game page.", "Wähle das Spielabbild auf der Seite Spiel.", "Scegli l'immagine del gioco nella pagina Gioco.", "Choisis l'image du jeu dans la page Jeu.", "「ゲーム」ページでゲームイメージを選んでください。", "请在“游戏”页面选择游戏镜像。" } },
            { "No se pudo arrancar el juego: ", new[] { "The game could not start: ", "Das Spiel konnte nicht starten: ", "Impossibile avviare il gioco: ", "Le jeu n'a pas pu démarrer : ", "ゲームを起動できませんでした：", "无法启动游戏：" } },
            { "El juego se cerró con el código {0}.\n\nÚltimas líneas de build\\run.err:\n{1}", new[] { "The game closed with code {0}.\n\nLast lines of build\\run.err:\n{1}", "Das Spiel wurde mit Code {0} beendet.\n\nLetzte Zeilen von build\\run.err:\n{1}", "Il gioco si è chiuso con il codice {0}.\n\nUltime righe di build\\run.err:\n{1}", "Le jeu s'est fermé avec le code {0}.\n\nDernières lignes de build\\run.err :\n{1}", "ゲームがコード{0}で終了しました。\n\nbuild\\run.err の最後の行：\n{1}", "游戏以代码{0}退出。\n\nbuild\\run.err 的最后几行：\n{1}" } },
            { "Elige la imagen de Pandora's Tower (ISO)", new[] { "Choose the Pandora's Tower image (ISO)", "Abbild von Pandora's Tower wählen (ISO)", "Scegli l'immagine di Pandora's Tower (ISO)", "Choisis l'image de Pandora's Tower (ISO)", "Pandora's Towerのイメージ（ISO）を選択", "选择Pandora's Tower的镜像（ISO）" } },
            { "Imagen ISO de Wii (*.iso)|*.iso|Todos los archivos (*.*)|*.*", new[] { "Wii ISO image (*.iso)|*.iso|All files (*.*)|*.*", "Wii-ISO-Abbild (*.iso)|*.iso|Alle Dateien (*.*)|*.*", "Immagine ISO Wii (*.iso)|*.iso|Tutti i file (*.*)|*.*", "Image ISO Wii (*.iso)|*.iso|Tous les fichiers (*.*)|*.*", "Wii ISOイメージ (*.iso)|*.iso|すべてのファイル (*.*)|*.*", "Wii ISO镜像 (*.iso)|*.iso|所有文件 (*.*)|*.*" } },
            { "Esa imagen no sirve: ", new[] { "That image won't work: ", "Dieses Abbild geht nicht: ", "Quell'immagine non va bene: ", "Cette image ne convient pas : ", "そのイメージは使えません：", "该镜像不可用：" } },
            { "Juego listo: se lee directamente de la imagen.", new[] { "Game ready: it is read straight from the image.", "Spiel bereit: wird direkt aus dem Abbild gelesen.", "Gioco pronto: letto direttamente dall'immagine.", "Jeu prêt : lu directement depuis l'image.", "準備完了：イメージから直接読み込みます。", "游戏就绪：直接从镜像读取。" } },
            { "tiene que ser una ISO; conviértela en Dolphin (clic derecho en el juego > Convertir archivo > ISO)", new[] { "it must be an ISO; convert it in Dolphin (right-click the game > Convert File > ISO)", "es muss eine ISO sein; in Dolphin konvertieren (Rechtsklick > Datei konvertieren > ISO)", "deve essere una ISO; convertila in Dolphin (clic destro > Converti file > ISO)", "il faut une ISO ; convertis-la dans Dolphin (clic droit > Convertir le fichier > ISO)", "ISOが必要です。Dolphinで変換してください（右クリック > ファイルを変換 > ISO）", "必须是ISO；请在Dolphin中转换（右键 > 转换文件 > ISO）" } },
            { "no se puede leer", new[] { "it can't be read", "nicht lesbar", "non si può leggere", "illisible", "読み込めません", "无法读取" } },
            { "no es una imagen de disco de Wii", new[] { "it isn't a Wii disc image", "kein Wii-Disc-Abbild", "non è un'immagine di disco Wii", "ce n'est pas une image de disque Wii", "Wiiのディスクイメージではありません", "不是Wii光盘镜像" } },
            { "es el juego {0}, y el port está hecho para Pandora's Tower SX3EXJ, SX3P01, SX3J01", new[] { "it is the game {0}, and the port is made for Pandora's Tower SX3EXJ, SX3P01, SX3J01", "das ist das Spiel {0}; der Port ist für Pandora's Tower SX3EXJ, SX3P01, SX3J01", "è il gioco {0}, e il port è per Pandora's Tower SX3EXJ, SX3P01, SX3J01", "c'est le jeu {0}, et le portage est pour Pandora's Tower SX3EXJ, SX3P01, SX3J01", "これは{0}です。この移植はPandora's Tower SX3EXJ, SX3P01, SX3J01用です", "这是游戏{0}，本移植适用于Pandora's Tower SX3EXJ, SX3P01, SX3J01" } },
            { "es otra revisión del disco ({0})", new[] { "it's another disc revision ({0})", "andere Disc-Revision ({0})", "è un'altra revisione del disco ({0})", "c'est une autre révision du disque ({0})", "別のディスクリビジョンです（{0}）", "是另一版本的光盘（{0}）" } },
            { "{0}–{1} de {2}", new[] { "{0}–{1} of {2}", "{0}–{1} von {2}", "{0}–{1} di {2}", "{0}–{1} sur {2}", "{0}–{1} / {2}", "{0}–{1} / {2}" } },
            { "La versión japonesa no está compilada: ejecuta ./setup.sh con su ISO.", new[] { "The Japanese version isn't built: run ./setup.sh with its ISO.", "Die japanische Version ist nicht kompiliert: führe ./setup.sh mit ihrer ISO aus.", "La versione giapponese non è compilata: esegui ./setup.sh con la sua ISO.", "La version japonaise n'est pas compilée : lance ./setup.sh avec son ISO.", "日本版はまだコンパイルされていません：そのISOで ./setup.sh を実行してください。", "日版尚未编译：请用其ISO运行 ./setup.sh。" } },
            { "Shaders nuevos", new[] { "New shaders", "Neue Shader", "Shader nuovi", "Nouveaux shaders", "新しいシェーダー", "新着色器" } },
            { "En segundo plano (puede faltar algo)", new[] { "In the background (things may be missing)", "Im Hintergrund (es kann etwas fehlen)", "In background (può mancare qualcosa)", "En arrière-plan (il peut manquer des objets)", "バックグラウンド（一瞬欠けることあり）", "后台编译（可能暂时缺失）" } },
            { "Esperarlos (no falta nada)", new[] { "Wait for them (nothing missing)", "Abwarten (nichts fehlt)", "Attendili (non manca nulla)", "Les attendre (rien ne manque)", "待つ（欠けなし）", "等待编译（不缺失）" } }
        };
        public static string T(string es)
        {
            if (es == null || Lang == "es") return es;
            string[] t;
            int i = Array.IndexOf(Order, Lang);
            return i >= 0 && Texts.TryGetValue(es, out t) ? t[i] : es;
        }
        public static bool Cjk { get { return Lang == "ja" || Lang == "zh"; } }
        public static bool HasCjk(string s)
        {
            if (s == null) return false;
            foreach (char c in s) if (c >= 0x2E80 && c < 0xFFEF && !(c >= 0x2190 && c <= 0x21FF)) return true;
            return false;
        }
        // the system's language, if the launcher has it
        public static string FromSystem()
        {
            string two = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            foreach (string l in new[] { "es", "en", "de", "it", "fr", "ja", "zh" }) if (l == two) return l;
            return "en";
        }
    }

    class Choice
    {
        public string Key;
        public string Text;
        public object[] Args;                      // Text is a format (translated first)
        public bool Rec;                           // followed by "(recomendada)"
        public Choice(string key, string text) { Key = key; Text = text; }
        public string Display()
        {
            string s = Args != null ? string.Format(L.T(Text), Args) : L.T(Text);
            return Rec ? s + L.T("  (recomendada)") : s;
        }
    }

    class Option
    {
        public string Id;
        public string Label;
        public List<Choice> Choices = new List<Choice>();
        public int Index;
        public Option(string id, string label) { Id = id; Label = label; }
        public string Key { get { return Choices[Index].Key; } }
        public void Select(string key)
        {
            for (int i = 0; i < Choices.Count; i++)
                if (Choices[i].Key == key) { Index = i; return; }
        }
        public void Step(int d) { Index = (Index + d + Choices.Count) % Choices.Count; }
    }

    // una acción del juego: su línea en el archivo de teclas, las teclas o
    // botones del ratón que la pulsan y, en el Classic Controller, el botón del mando
    class Bind
    {
        public string Id, Label, Pad, DefaultPad;
        public List<string> Keys = new List<string>(), DefaultKeys = new List<string>();
        public bool HasPad;
        public Bind(string id, string label, string keys, string pad)
        {
            Id = id; Label = label;
            foreach (string k in keys.Split(',')) if (k.Trim().Length > 0) DefaultKeys.Add(k.Trim());
            Keys.AddRange(DefaultKeys);
            HasPad = pad != null;
            DefaultPad = Pad = pad;
        }
    }

    static class XInput
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct State
        {
            public uint Packet;
            public ushort Buttons;
            public byte LeftTrigger, RightTrigger;
            public short LX, LY, RX, RY;
        }
        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        static extern uint Get14(uint user, out State s);
        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        static extern uint Get910(uint user, out State s);
        static int api = 0;                       // 0 sin probar, 1 1_4, 2 9_1_0, -1 ninguna

        public static bool Read(out State s)
        {
            s = new State();
            try
            {
                if (api == 0 || api == 1)
                {
                    try { if (Get14(0, out s) == 0) { api = 1; return true; } if (api == 1) return false; }
                    catch (DllNotFoundException) { }
                    catch (EntryPointNotFoundException) { }
                }
                if (api == 0 || api == 2)
                {
                    if (Get910(0, out s) == 0) { api = 2; return true; }
                    if (api == 2) return false;
                }
            }
            catch (Exception) { api = -1; }
            return false;
        }

        // los botones de XInput con los nombres del archivo de teclas (Pad X = ...)
        public static readonly ushort[] Bits = { 0x1000, 0x2000, 0x4000, 0x8000, 0x0100, 0x0200, 0x0010, 0x0020, 0x0040, 0x0080, 0x0001, 0x0002, 0x0004, 0x0008 };
        public static readonly string[] Names = { "South", "East", "West", "North", "Left Shoulder", "Right Shoulder", "Start", "Back", "Left Stick", "Right Stick", "Dpad Up", "Dpad Down", "Dpad Left", "Dpad Right" };
    }

    class LauncherForm : Form
    {
        // ---- paleta y tipos
        static readonly Color Gold = Color.FromArgb(222, 186, 112);
        static readonly Color GoldDim = Color.FromArgb(150, 124, 78);
        static readonly Color Ivory = Color.FromArgb(238, 230, 214);
        static readonly Color Muted = Color.FromArgb(170, 162, 176);
        static readonly Color Silver = Color.FromArgb(232, 232, 240);
        static readonly Color Ink = Color.FromArgb(14, 10, 10);

        const int W = 1120, H = 630, SideW = 390, PadX = 36;

        readonly string root, build;
        readonly Image background, logo;
        readonly PrivateFontCollection fonts = new PrivateFontCollection();
        FontFamily gothic, gothicLatin;
        string serifName, cjkName;
        Font labelFont, valueFont, tabFont, buttonFont, playFont, hintFont, smallFont, glyphFont, titleFont;

        // páginas: 0 Gráficos, 1 Controles, 2 Juego
        static readonly string[] Pages = { "Gráficos", "Texturas", "Controles", "Juego" };
        int page = 0;
        readonly List<Option>[] pageOptions = { new List<Option>(), new List<Option>(), new List<Option>(), new List<Option>() };
        readonly Dictionary<string, Option> optionById = new Dictionary<string, Option>();
        readonly List<Bind> classicBinds = new List<Bind>(), remoteBinds = new List<Bind>();
        string gameDir = "";                       // la carpeta del juego preparada ("" = la de setup.sh)
        string isoPath = "";                       // la imagen de la que salió

        // preparar el juego desde la imagen: un hilo aparte (wiikit.disc, con el Python de MSYS2)

        // elementos que se pueden señalar: se rehacen en cada dibujo
        enum Kind { Tab, Option, Action, Play, Exit, Close, BindKeys, BindPad, PanelButton }
        class Item { public Kind Kind; public Rectangle R; public int Index; public string Id; }
        List<Item> items = new List<Item>();
        int focus = 0, hover = -1;
        int infoY = -1;                            // dónde va la carpeta del juego (página Juego)

        bool panel;                                // el panel de asignar botones
        int panelScroll;
        Bind capture; bool captureIsPad; bool captureArmed;
        string status = "";
        Point dragFrom; bool dragging;
        readonly Timer padTimer = new Timer();
        ushort padPrev; int padRepeat;

        public LauncherForm(string rootDir)
        {
            root = rootDir;
            build = Path.Combine(root, "build");
            Text = "Pandora's Tower";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(W, H);
            DoubleBuffered = true;
            KeyPreview = true;
            BackColor = Ink;

            // el icono: el tuyo (arte\icono.ico), el del banner de tu disco (build\arte) o el del port
            string ico = FirstExisting(Path.Combine(root, "arte", "icono.ico"),
                                       Path.Combine(build, "arte", "icono.ico"),
                                       Path.Combine(root, "tools", "lanzador", "pandora.ico"));
            if (ico != null) { try { Icon = new Icon(ico); } catch (Exception) { } }
            background = LoadImage(FirstExisting(Path.Combine(root, "arte", "fondo.png"),
                                                 Path.Combine(root, "tools", "lanzador", "fondo.png"),
                                                 Path.Combine(build, "arte", "fondo.png")));
            logo = LoadImage(FirstExisting(Path.Combine(root, "arte", "logo.png"),
                                           Path.Combine(root, "tools", "lanzador", "logo.png")));   // (el port no trae logo: el título va en letra)

            string serif = PickFont("Palatino Linotype", "Book Antiqua", "P052", "Constantia", "Georgia", "DejaVu Serif");
            // letra de las etiquetas, pestañas y botones: Pirata One (licencia OFL), gótica y
            // afilada como la del logo (instalada en el sistema, o la del paquete sin instalarla)
            if (PickFont("Pirata One") == "Pirata One") gothic = new FontFamily("Pirata One");
            string ttf = FirstExisting(Path.Combine(root, "tools", "lanzador", "PirataOne-Regular.ttf"));
            if (gothic == null && ttf != null) { try { fonts.AddFontFile(ttf); gothic = fonts.Families[0]; } catch (Exception) { } }
            if (gothic == null) gothic = new FontFamily(serif);
            gothicLatin = gothic;
            serifName = serif;
            glyphFont = new Font(PickFont("Segoe UI Symbol", "DejaVu Sans"), 11);
            titleFont = new Font(gothic, 56, FontStyle.Regular, GraphicsUnit.Pixel);

            BuildOptions();
            BuildBinds();
            LoadSettings();
            LoadControls();
            L.Lang = Get("idioma").Key;
            SetupFonts();
            if (PatchesPending()) status = L.T("Hay arreglos nuevos sin compilar: ejecuta ./setup.sh en MSYS2 CLANG64.");

            padTimer.Interval = 40;
            padTimer.Tick += delegate
            {
                PollPad();
            };
            padTimer.Start();
        }

        // las letras del idioma: la gótica (Pirata One) y la serif no tienen japonés ni
        // chino; con esos idiomas, una de Windows que sí los tenga
        void SetupFonts()
        {
            string cjk = L.Lang == "zh" ? PickFont("Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Yu Gothic UI", "Meiryo UI", "Noto Sans CJK SC", "Droid Sans Fallback")
                                        : PickFont("Yu Gothic UI", "Meiryo UI", "MS UI Gothic", "Microsoft YaHei UI", "Noto Sans CJK JP", "Droid Sans Fallback");
            cjkName = cjk;
            FontFamily head = L.Cjk ? new FontFamily(cjk) : gothicLatin;
            string body = L.Cjk ? cjk : serifName;
            float k = L.Cjk ? 0.85f : 1f;                                // las letras CJK son más grandes
            labelFont = new Font(head, 21 * k, L.Cjk ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            tabFont = new Font(head, 21 * k, L.Cjk ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            buttonFont = new Font(head, 21 * k, L.Cjk ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            playFont = new Font(head, 32 * k, L.Cjk ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            valueFont = new Font(body, 12.5f * (L.Cjk ? 0.95f : 1f), FontStyle.Bold);
            smallFont = new Font(body, 10.5f);
            hintFont = new Font(body, 9.5f);
        }

        // un texto con japonés o chino en una letra que no los tiene: la de esos idiomas
        Font FontFor(Font f, string text)
        {
            if (L.Cjk || !L.HasCjk(text)) return f;
            if (cjkName == null) cjkName = PickFont("Yu Gothic UI", "Meiryo UI", "Microsoft YaHei UI", "MS UI Gothic", "Noto Sans CJK JP", "Droid Sans Fallback");
            return new Font(cjkName, f.Size, f.Style, f.Unit);
        }

        static string FirstExisting(params string[] paths)
        {
            foreach (string p in paths) if (p != null && File.Exists(p)) return p;
            return null;
        }

        static Image LoadImage(string path)
        {
            if (path == null) return null;
            try { using (var s = new MemoryStream(File.ReadAllBytes(path))) return new Bitmap(Image.FromStream(s)); }
            catch (Exception) { return null; }
        }

        static string PickFont(params string[] names)
        {
            var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (FontFamily f in FontFamily.Families) have.Add(f.Name);
            foreach (string n in names) if (have.Contains(n)) return n;
            return FontFamily.GenericSerif.Name;
        }

        // ---- opciones -------------------------------------------------------------------------
        void Add(int pg, Option o) { pageOptions[pg].Add(o); optionById[o.Id] = o; }
        Option Get(string id) { return optionById[id]; }

        void BuildOptions()
        {
            int screenH = Screen.PrimaryScreen.Bounds.Height;
            int native = Math.Max(1, (int)Math.Round(screenH / 528.0));
            int best = Math.Min(5, Math.Max(2, (int)Math.Round(2.0 * screenH / 528.0)));

            var res = new Option("resolucion", "Resolución");
            res.Choices.Add(new Choice("wii", "Nativa de Wii · 640 × 528"));
            res.Choices.Add(new Choice("pantalla", "Nativa de tu pantalla · x{0} · {1} × {2}") { Args = new object[] { native, 640 * native, 528 * native } });
            for (int s = 2; s <= 5; s++)
                res.Choices.Add(new Choice(s.ToString(), "x{0} · {1} × {2}") { Args = new object[] { s, 640 * s, 528 * s }, Rec = s == best });
            res.Select(best.ToString());
            Add(0, res);

            var fps = new Option("fps", "Fotogramas");
            fps.Choices.Add(new Choice("30", "30 fps (original)"));
            // por encima de 30, el juego sigue a sus 30 (su física está hecha para
            // ellos: a 60 de verdad la capa del personaje se queda rígida) y los
            // fotogramas intermedios se interpolan
            fps.Choices.Add(new Choice("60", "60 fps (interpolado)"));
            fps.Choices.Add(new Choice("90", "90 fps (interpolado)"));
            fps.Choices.Add(new Choice("120", "120 fps (interpolado)"));
            fps.Choices.Add(new Choice("monitor", "Al ritmo de tu monitor (interpolado)"));
            fps.Choices.Add(new Choice("max", "Ilimitado (interpolado)"));
            fps.Choices.Add(new Choice("60n", "60 fps nativos (capa rígida)"));
            fps.Select("60");
            Add(0, fps);

            var win = new Option("pantalla", "Pantalla");
            win.Choices.Add(new Choice("completa", "Completa sin bordes"));
            win.Choices.Add(new Choice("exclusiva", "Completa exclusiva"));
            win.Choices.Add(new Choice("ventana", "En ventana"));
            Add(0, win);

            // las bandas negras: arriba y abajo quedan las líneas que la Wii no muestra
            // (el juego usa menos de 480); la intro y las cinemáticas traen las suyas
            // dentro de la imagen, que wiiboot detecta y recorta (WIIKIT_LETTERBOX)
            var fit = new Option("bandas", "Bandas negras");
            fit.Choices.Add(new Choice("tv", "Como en la consola"));
            fit.Choices.Add(new Choice("llenar", "Quitar: llenar la pantalla"));
            fit.Choices.Add(new Choice("estirar", "Quitar: estirar la imagen"));
            fit.Choices.Add(new Choice("cine", "Quitar todas: 16:9 completo"));
            Add(0, fit);

            // los shaders nuevos: esperándolos (nada deja de dibujarse; alguna pausa la primera
            // vez que sale uno, y los ya vistos se preparan al arrancar) o en segundo plano
            // (sin pausas, pero lo que los usa no se dibuja hasta que están: con la caché
            // vacía, tras instalar, desaparecen cosas un momento)
            var sh = new Option("shaders", "Shaders nuevos");
            sh.Choices.Add(new Choice("wait", "Esperarlos (no falta nada)"));
            sh.Choices.Add(new Choice("async", "En segundo plano (puede faltar algo)"));
            Add(0, sh);

            var tex = new Option("texturas", "Filtrado de texturas");
            tex.Choices.Add(new Choice("16", "x16 (el más nítido)"));
            tex.Choices.Add(new Choice("12", "x12"));
            tex.Choices.Add(new Choice("8", "x8"));
            tex.Choices.Add(new Choice("4", "x4"));
            tex.Choices.Add(new Choice("2", "x2"));
            tex.Choices.Add(new Choice("1", "Original (sin filtrado)"));
            Add(1, tex);

            // paquetes de texturas: cada carpeta dentro de texturas/ (los PNG de un paquete de
            // Dolphin sirven tal cual, en sus subcarpetas)
            var pk = new Option("paquete", "Paquete de texturas");
            pk.Choices.Add(new Choice("ninguno", "Ninguno (las originales)"));
            try
            {
                string dir = Path.Combine(root, "texturas");
                if (Directory.Exists(dir))
                {
                    var names = new List<string>();
                    foreach (string d in Directory.GetDirectories(dir)) names.Add(Path.GetFileName(d));
                    names.Sort(StringComparer.OrdinalIgnoreCase);
                    foreach (string n in names) pk.Choices.Add(new Choice(n, n));
                }
            }
            catch (Exception) { }
            Add(1, pk);

            var ctl = new Option("controles", "Esquema de control");
            ctl.Choices.Add(new Choice("mando", "Mando (Classic Controller)"));
            ctl.Choices.Add(new Choice("raton", "Ratón y teclado (Wii Remote + Nunchuk)"));
            Add(2, ctl);

            var icons = new Option("iconos", "Iconos de los botones");
            icons.Choices.Add(new Choice("wii", "Originales (Wii)"));
            icons.Choices.Add(new Choice("xbox", "Xbox"));
            icons.Choices.Add(new Choice("ps", "PlayStation"));
            icons.Choices.Add(new Choice("teclado", "Teclado y ratón"));
            Add(2, icons);

            // agitar el Wii Remote (ratón y teclado): con su tecla o botón, y también
            // moviendo el ratón rápido de un lado a otro (Mouse Shake, en controles.txt)
            var shake = new Option("agitar", "Agitar el Wii Remote");
            shake.Choices.Add(new Choice("raton", "Tecla y ratón agitado"));
            shake.Choices.Add(new Choice("tecla", "Solo su tecla"));
            shake.Select("raton");
            Add(2, shake);

            var dz = new Option("zona", "Zona muerta de los sticks");
            foreach (string z in new[] { "0.05", "0.10", "0.15", "0.20", "0.25", "0.30" })
                dz.Choices.Add(new Choice(z, (int)(double.Parse(z, System.Globalization.CultureInfo.InvariantCulture) * 100) + " %"));
            dz.Select("0.15");
            Add(2, dz);

            // el idioma de la consola (SYSCONF): el juego usa ese si su disco lo trae
            // el idioma: el del lanzador y el de la consola (el juego usa ese si su
            // disco lo trae); cada uno con su nombre en su lengua
            var lang = new Option("idioma", "Idioma");
            lang.Choices.Add(new Choice("es", "Español"));
            lang.Choices.Add(new Choice("en", "English"));
            lang.Choices.Add(new Choice("de", "Deutsch"));
            lang.Choices.Add(new Choice("it", "Italiano"));
            lang.Choices.Add(new Choice("fr", "Français"));
            lang.Choices.Add(new Choice("ja", "日本語"));
            lang.Choices.Add(new Choice("zh", "中文"));
            lang.Select(L.FromSystem());
            Add(3, lang);

            var strap = new Option("correa", "Pantalla de la correa");
            strap.Choices.Add(new Choice("omitir", "Omitir al arrancar"));
            strap.Choices.Add(new Choice("mostrar", "Mostrar, como en la Wii"));
            Add(3, strap);
        }

        void BuildBinds()
        {
            // el Classic Controller: teclado de respaldo y el mando (por posición, como el Classic)
            string[,] cl = {
                { "A", "A", "Return, Space", "East" }, { "B", "B", "Backspace, C", "South" },
                { "X", "X", "R", "North" }, { "Y", "Y", "F", "West" },
                { "L", "L", "Left Shift", "Left Shoulder" }, { "R", "R", "E", "Right Shoulder" },
                { "ZL", "ZL", "Mouse Right", "Left Trigger" }, { "ZR", "ZR", "Mouse Left", "Right Trigger" },
                { "Plus", "+", "Tab", "Start" }, { "Minus", "−", "Q", "Back" },
                { "Up", "Cruceta ↑", "Up", "Dpad Up" }, { "Down", "Cruceta ↓", "Down", "Dpad Down" },
                { "Left", "Cruceta ←", "Left", "Dpad Left" }, { "Right", "Cruceta →", "Right", "Dpad Right" },
                { "Left Stick Up", "Stick izq. ↑", "W", null }, { "Left Stick Down", "Stick izq. ↓", "S", null },
                { "Left Stick Left", "Stick izq. ←", "A", null }, { "Left Stick Right", "Stick izq. →", "D", null },
                { "Right Stick Up", "Stick der. ↑", "", null }, { "Right Stick Down", "Stick der. ↓", "", null },
                { "Right Stick Left", "Stick der. ←", "", null }, { "Right Stick Right", "Stick der. →", "", null } };
            for (int i = 0; i < cl.GetLength(0); i++) classicBinds.Add(new Bind(cl[i, 0], cl[i, 1], cl[i, 2], cl[i, 3]));
            // el Wii Remote y el Nunchuk: el ratón es el puntero; el stick del Nunchuk en WASD
            string[,] rm = {
                { "A", "A", "Return, Keypad Enter, Mouse Left" }, { "B", "B", "Backspace, Mouse Right" },
                { "Up", "Cruceta ↑", "Up" }, { "Down", "Cruceta ↓", "Down" }, { "Left", "Cruceta ←", "Left" }, { "Right", "Cruceta →", "Right" },
                { "Plus", "+", "Tab" }, { "Minus", "−", "Q" }, { "1", "1", "1" }, { "2", "2", "2" },
                { "Shake", "Agitar", "Space, Mouse Middle" }, { "C", "Nunchuk C", "E" }, { "Z", "Nunchuk Z", "Left Shift" },
                { "Stick Up", "Stick ↑", "W" }, { "Stick Down", "Stick ↓", "S" }, { "Stick Left", "Stick ←", "A" },
                { "Stick Right", "Stick →", "D" }, { "Walk", "Caminar", "Left Ctrl" },
                { "Nunchuk Shake", "Agitar el Nunchuk", "F" } };
            for (int i = 0; i < rm.GetLength(0); i++) remoteBinds.Add(new Bind(rm[i, 0], rm[i, 1], rm[i, 2], null));
        }

        List<Bind> CurrentBinds { get { return Get("controles").Key == "raton" ? remoteBinds : classicBinds; } }

        string IniPath { get { return Path.Combine(root, "lanzador.ini"); } }
        string ControlsPath { get { return Path.Combine(root, "controles.txt"); } }

        void LoadSettings()
        {
            if (!File.Exists(IniPath)) return;
            try
            {
                foreach (string line in File.ReadAllLines(IniPath, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                    if (k == "juego") { gameDir = v; continue; }
                    if (k == "iso") { isoPath = v; continue; }
                    Option o;
                    if (optionById.TryGetValue(k, out o)) o.Select(v);
                }
            }
            catch (Exception) { }
        }

        void SaveSettings()
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var list in pageOptions) foreach (Option o in list) sb.AppendLine(o.Id + "=" + o.Key);
                if (gameDir.Length > 0) sb.AppendLine("juego=" + gameDir);
                if (isoPath.Length > 0) sb.AppendLine("iso=" + isoPath);
                File.WriteAllText(IniPath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception) { }
        }

        // controles.txt: el archivo de teclas de wiiboot ([Wii Remote], [Classic Controller], Pad X = ...)
        void LoadControls()
        {
            if (!File.Exists(ControlsPath)) return;
            try
            {
                bool classic = false;
                var seenPad = false;
                foreach (string raw in File.ReadAllLines(ControlsPath, Encoding.UTF8))
                {
                    string l = raw.Trim();
                    if (l.Length == 0 || l[0] == '#') continue;
                    if (l[0] == '[') { classic = l.IndexOf("Classic", StringComparison.OrdinalIgnoreCase) >= 0; continue; }
                    int eq = l.IndexOf('=');
                    if (eq < 0) continue;
                    string k = l.Substring(0, eq).Trim(), v = l.Substring(eq + 1).Trim();
                    if (classic && k == "Dead Zone") { Get("zona").Select(v); continue; }
                    if (classic && (k == "Input" || k == "Face Buttons")) continue;
                    var list = new List<string>();
                    foreach (string p in v.Split(',')) if (p.Trim().Length > 0) list.Add(p.Trim());
                    if (classic && k.StartsWith("Pad "))
                    {
                        if (!seenPad) { foreach (Bind b in classicBinds) if (b.HasPad) b.Pad = null; seenPad = true; }
                        Bind b2 = FindBind(classicBinds, k.Substring(4).Trim());
                        if (b2 != null && b2.HasPad) b2.Pad = list.Count > 0 ? list[0] : null;
                        continue;
                    }
                    Bind b3 = FindBind(classic ? classicBinds : remoteBinds, k);
                    list.RemoveAll(x => string.Equals(x, "Mouse Shake", StringComparison.OrdinalIgnoreCase));   // es la opción "Agitar"
                    if (b3 != null) { b3.Keys.Clear(); b3.Keys.AddRange(list); }
                }
            }
            catch (Exception) { }
        }

        static Bind FindBind(List<Bind> list, string id)
        {
            foreach (Bind b in list) if (string.Equals(b.Id, id, StringComparison.OrdinalIgnoreCase)) return b;
            return null;
        }

        void SaveControls()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Controles de Pandora's Tower: los escribe el lanzador (PandorasTower.exe), que se los pasa a wiiboot.");
            sb.AppendLine("# Formato del archivo de teclas de wiiboot; las líneas \"Pad X = ...\" asignan los botones del mando.");
            sb.AppendLine("[Wii Remote]");
            foreach (Bind b in remoteBinds)
            {
                var keys = new List<string>(b.Keys);
                if (b.Id == "Shake" && Get("agitar").Key == "raton") keys.Add("Mouse Shake");   // el ratón movido rápido
                sb.AppendLine(b.Id + " = " + string.Join(", ", keys.ToArray()));
            }
            sb.AppendLine();
            sb.AppendLine("[Classic Controller]");
            sb.AppendLine("Input        = Auto");
            sb.AppendLine("Face Buttons = Position");
            sb.AppendLine("Dead Zone    = " + Get("zona").Key);
            foreach (Bind b in classicBinds) sb.AppendLine(b.Id + " = " + string.Join(", ", b.Keys.ToArray()));
            foreach (Bind b in classicBinds) if (b.HasPad) sb.AppendLine("Pad " + b.Id + " = " + (b.Pad ?? ""));
            try { File.WriteAllText(ControlsPath, sb.ToString(), new UTF8Encoding(false)); }
            catch (Exception ex) { status = L.T("No se pudieron guardar los controles: ") + ex.Message; }
        }

        // nombres para mostrar de los botones del mando
        static string PadLabel(string n)
        {
            if (n == null) return "—";
            switch (n)
            {
                case "South": return "A  (abajo)";
                case "East": return "B  (derecha)";
                case "West": return "X  (izquierda)";
                case "North": return "Y  (arriba)";
                case "Left Shoulder": return "LB";
                case "Right Shoulder": return "RB";
                case "Left Trigger": return "LT";
                case "Right Trigger": return "RT";
                case "Start": return "Start / Menú";
                case "Back": return "Back / Vista";
                case "Guide": return "Botón central";
                case "Left Stick": return "L3 (stick izq.)";
                case "Right Stick": return "R3 (stick der.)";
                case "Dpad Up": return "Cruceta ↑";
                case "Dpad Down": return "Cruceta ↓";
                case "Dpad Left": return "Cruceta ←";
                case "Dpad Right": return "Cruceta →";
            }
            return n;
        }
        static readonly string[] PadCycle = { "South", "East", "West", "North", "Left Shoulder", "Right Shoulder", "Left Trigger", "Right Trigger",
                                              "Start", "Back", "Guide", "Left Stick", "Right Stick", "Dpad Up", "Dpad Down", "Dpad Left", "Dpad Right" };

        static string KeysLabel(List<string> keys)
        {
            if (keys.Count == 0) return "—";
            var parts = new List<string>();
            foreach (string k in keys) parts.Add(L.T(KeyLabel(k)));
            return string.Join(", ", parts.ToArray());
        }
        static string KeyLabel(string k)
        {
            switch (k)
            {
                case "Return": return "Intro";
                case "Keypad Enter": return "Intro (numérico)";
                case "Space": return "Espacio";
                case "Backspace": return "Retroceso";
                case "Left Shift": return "Mayús izq.";
                case "Right Shift": return "Mayús der.";
                case "Left Ctrl": return "Ctrl izq.";
                case "Right Ctrl": return "Ctrl der.";
                case "Left Alt": return "Alt";
                case "Tab": return "Tab";
                case "Up": return "↑";
                case "Down": return "↓";
                case "Left": return "←";
                case "Right": return "→";
                case "Mouse Left": return "Ratón izq.";
                case "Mouse Right": return "Ratón der.";
                case "Mouse Middle": return "Ratón central";
                case "Mouse X1": return "Ratón X1";
                case "Mouse X2": return "Ratón X2";
            }
            return k;
        }

        // una tecla de Windows con el nombre de SDL que entiende wiiboot (null: no se puede usar)
        static string SdlKeyName(Keys k)
        {
            if (k >= Keys.A && k <= Keys.Z) return ((char)('A' + (k - Keys.A))).ToString();
            if (k >= Keys.D0 && k <= Keys.D9) return ((char)('0' + (k - Keys.D0))).ToString();
            if (k >= Keys.NumPad0 && k <= Keys.NumPad9) return "Keypad " + (char)('0' + (k - Keys.NumPad0));
            if (k >= Keys.F1 && k <= Keys.F10) return "F" + (1 + (k - Keys.F1));
            switch (k)
            {
                case Keys.Return: return "Return";
                case Keys.Space: return "Space";
                case Keys.Tab: return "Tab";
                case Keys.Back: return "Backspace";
                case Keys.ShiftKey: case Keys.LShiftKey: return "Left Shift";
                case Keys.RShiftKey: return "Right Shift";
                case Keys.ControlKey: case Keys.LControlKey: return "Left Ctrl";
                case Keys.RControlKey: return "Right Ctrl";
                case Keys.Menu: case Keys.LMenu: return "Left Alt";
                case Keys.RMenu: return "Right Alt";
                case Keys.Up: return "Up";
                case Keys.Down: return "Down";
                case Keys.Left: return "Left";
                case Keys.Right: return "Right";
                case Keys.Insert: return "Insert";
                case Keys.Home: return "Home";
                case Keys.End: return "End";
                case Keys.PageUp: return "PageUp";
                case Keys.PageDown: return "PageDown";
                case Keys.Oemcomma: return "Comma";
                case Keys.OemPeriod: return "Period";
                case Keys.OemMinus: return "Minus";
                case Keys.Oemplus: return "Equals";
                case Keys.Oem1: return "Semicolon";
                case Keys.Oem2: return "Slash";
                case Keys.Oem3: return "Grave";
                case Keys.Oem4: return "Left Bracket";
                case Keys.Oem5: return "Backslash";
                case Keys.Oem6: return "Right Bracket";
                case Keys.Oem7: return "Apostrophe";
                case Keys.Oem102: return "NonUSBackslash";
                case Keys.Add: return "Keypad +";
                case Keys.Subtract: return "Keypad -";
                case Keys.Multiply: return "Keypad *";
                case Keys.Divide: return "Keypad /";
                case Keys.Decimal: return "Keypad .";
            }
            return null;
        }

        // ---- la carpeta del juego -------------------------------------------------------------
        const string DolSha1 = "bdbd1e9153220a3a0b092f8319127f59093d3c16";

        string GameDir()
        {
            if (gameDir.Length > 0) return gameDir;
            string pathFile = Path.Combine(build, "extract_path.txt");
            return File.Exists(pathFile) ? File.ReadAllText(pathFile).Trim() : "";
        }

        // la carpeta de "Extraer todo el disco" de Dolphin: la que tiene sys/ y files/, o DATA dentro
        static string CheckGameDir(string dir, out string error)
        {
            error = null;
            if (!File.Exists(Path.Combine(dir, "sys", "main.dol")) && File.Exists(Path.Combine(dir, "DATA", "sys", "main.dol")))
                dir = Path.Combine(dir, "DATA");
            foreach (string f in new[] { "sys/main.dol", "sys/boot.bin", "sys/bi2.bin", "sys/apploader.img", "sys/fst.bin", "tmd.bin" })
                if (!File.Exists(Path.Combine(dir, f.Replace('/', Path.DirectorySeparatorChar))))
                { error = "falta " + f + ": extrae el disco con \"Extraer todo el disco\" de Dolphin"; return null; }
            if (!Directory.Exists(Path.Combine(dir, "files"))) { error = "falta la carpeta files"; return null; }
            using (var sha = SHA1.Create())
            using (var f = File.OpenRead(Path.Combine(dir, "sys", "main.dol")))
            {
                string h = BitConverter.ToString(sha.ComputeHash(f)).Replace("-", "").ToLowerInvariant();
                if (h != DolSha1) { error = "este main.dol es de otra versión del juego; el port necesita la SX3EXJ"; return null; }
            }
            return dir;
        }

        // los arreglos de tools\patches que ./setup.sh aún no ha aplicado (y compilado):
        // los mismos que comprueba setup.sh, por la huella de todos juntos
        bool PatchesPending()
        {
            try
            {
                string dir = Path.Combine(root, Path.Combine("tools", "patches"));
                string mark = Path.Combine(root, Path.Combine("wiikit", ".pandora-patches"));
                if (!Directory.Exists(dir) || !File.Exists(mark)) return false;
                string[] files = Directory.GetFiles(dir, "*.patch");
                Array.Sort(files, StringComparer.Ordinal);
                using (var sha = System.Security.Cryptography.SHA1.Create())
                {
                    foreach (string f in files) { byte[] b = File.ReadAllBytes(f); sha.TransformBlock(b, 0, b.Length, null, 0); }
                    sha.TransformFinalBlock(new byte[0], 0, 0);
                    string hex = BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
                    string[] w = File.ReadAllText(mark).Trim().Split(' ');
                    if (w.Length < 2 || w[1] != hex) return true;
                    // aplicados, pero alguna versión compilada antes no se recompiló después
                    // (./setup.sh no llegó al final, o se compiló solo otra versión)
                    foreach (string v in new[] { build, Path.Combine(build, "jp") })
                    {
                        string exe = Path.Combine(v, Path.Combine("recomp-build", "wiiboot.exe"));
                        if (File.Exists(exe) && File.GetLastWriteTimeUtc(mark) > File.GetLastWriteTimeUtc(exe)) return true;
                    }
                    return false;
                }
            }
            catch (Exception) { return false; }
        }

        // la imagen se lee directamente (wiiboot la descifra según lee): solo se comprueba
        // que sea una ISO de Wii de Pandora's Tower SX3EXJ, revisión 0
        void ChooseIso()
        {
            string iso;
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = L.T("Elige la imagen de Pandora's Tower (ISO)");
                dlg.Filter = L.T("Imagen ISO de Wii (*.iso)|*.iso|Todos los archivos (*.*)|*.*");
                if (isoPath.Length > 0 && File.Exists(isoPath)) dlg.InitialDirectory = Path.GetDirectoryName(isoPath);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                iso = dlg.FileName;
            }
            string err = CheckIso(iso);
            if (err != null) { status = L.T("Esa imagen no sirve: ") + err + "."; return; }
            gameDir = iso;
            isoPath = iso;
            SaveSettings();
            status = L.T("Juego listo: se lee directamente de la imagen.");
        }

        // el identificador del disco: de la ISO, o del boot.bin de una carpeta extraída
        static string GameId(string path)
        {
            try
            {
                string f = File.Exists(path) ? path : Path.Combine(path, "sys", "boot.bin");
                var h = new byte[6];
                using (var fs = File.OpenRead(f)) if (fs.Read(h, 0, 6) != 6) return "";
                return Encoding.ASCII.GetString(h);
            }
            catch (Exception) { return ""; }
        }

        static string CheckIso(string iso)
        {
            try
            {
                string ext = Path.GetExtension(iso).ToLowerInvariant();
                if (ext == ".wbfs" || ext == ".rvz" || ext == ".wia" || ext == ".gcz" || ext == ".ciso")
                    return "tiene que ser una ISO; conviértela en Dolphin (clic derecho en el juego > Convertir archivo > ISO)";
                var h = new byte[0x20];
                using (var f = File.OpenRead(iso)) { if (f.Read(h, 0, h.Length) != h.Length) return L.T("no se puede leer"); }
                uint magic = (uint)(h[0x18] << 24 | h[0x19] << 16 | h[0x1A] << 8 | h[0x1B]);
                if (magic != 0x5D1C9EA3u) return L.T("no es una imagen de disco de Wii");
                string id = Encoding.ASCII.GetString(h, 0, 6);
                // la norteamericana (SX3EXJ) y la europea (SX3P01) llevan el mismo ejecutable
                if (id != "SX3EXJ" && id != "SX3P01" && id != "SX3J01") return string.Format(L.T("es el juego {0}, y el port está hecho para Pandora's Tower SX3EXJ, SX3P01, SX3J01"), id);
                if (h[7] != 0) return string.Format(L.T("es otra revisión del disco ({0})"), h[7]);
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ---- disposición y dibujo -------------------------------------------------------------
        const int LogoTop = 20, LogoMaxH = 178;
        int LogoBottom { get { return logo != null ? LogoTop + (int)Math.Min(LogoMaxH, logo.Height * (float)(SideW - 2 * PadX + 20) / logo.Width) : 130; } }

        void BuildItems()
        {
            items = new List<Item>();
            if (panel) { LayoutPanel(); return; }
            int y = LogoBottom + 10;
            int tx = PadX;
            using (var bmp = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bmp))
                for (int i = 0; i < Pages.Length; i++)
                {
                    int w = (int)g.MeasureString(L.T(Pages[i]), tabFont).Width + 10;
                    items.Add(new Item { Kind = Kind.Tab, R = new Rectangle(tx - 4, y, w, 34), Index = i });
                    tx += w + 8;
                }
            y += 48;
            List<Option> opts = pageOptions[page];
            int rowH = opts.Count >= 5 ? 52 : 58;                    // cinco opciones caben sobre Jugar
            for (int i = 0; i < opts.Count; i++, y += rowH)
                items.Add(new Item { Kind = Kind.Option, R = new Rectangle(PadX - 22, y, SideW - PadX + 4, rowH - 4), Index = i, Id = opts[i].Id });
            infoY = -1;
            if (page == 3) { infoY = y + 2; y += 54; }
            string[] actions = page == 2 ? new[] { "Asignar botones y teclas" }
                             : page == 3 ? new[] { "Elegir la imagen del juego" }
                             : new string[0];
            for (int i = 0; i < actions.Length; i++, y += 44)
                items.Add(new Item { Kind = Kind.Action, R = new Rectangle(PadX, y + 4, SideW - 2 * PadX, 36), Index = i, Id = actions[i] });
            if (page == 3)
            {
                int half = (SideW - 2 * PadX - 10) / 2;
                items.Add(new Item { Kind = Kind.Action, R = new Rectangle(PadX, y + 4, half, 36), Index = 1, Id = "Copiar partidas" });
                items.Add(new Item { Kind = Kind.Action, R = new Rectangle(PadX + half + 10, y + 4, half, 36), Index = 2, Id = "Abrir partidas" });
                y += 44;
            }
            items.Add(new Item { Kind = Kind.Play, R = new Rectangle(PadX, H - 112, SideW - 2 * PadX, 56) });
            items.Add(new Item { Kind = Kind.Exit, R = new Rectangle(PadX, H - 48, 90, 28) });
            items.Add(new Item { Kind = Kind.Close, R = new Rectangle(W - 46, 12, 34, 30) });
        }

        Rectangle PanelRect { get { return new Rectangle(SideW + 34, 34, W - SideW - 68, H - 68); } }
        const int RowH = 30, PanelHead = 92;
        int VisibleRows { get { return (PanelRect.Height - PanelHead - 96) / RowH; } }

        void LayoutPanel()
        {
            Rectangle pr = PanelRect;
            List<Bind> binds = CurrentBinds;
            bool pad = binds == classicBinds;
            int colKeys = pr.X + 210, colPad = pr.X + pr.Width - 230;
            int keysW = (pad ? colPad - 16 : pr.Right - 24) - colKeys;
            int n = Math.Min(VisibleRows, binds.Count - panelScroll);
            for (int i = 0; i < n; i++)
            {
                int bi = panelScroll + i, y = pr.Y + PanelHead + i * RowH;
                items.Add(new Item { Kind = Kind.BindKeys, R = new Rectangle(colKeys, y, keysW, RowH - 4), Index = bi });
                if (pad && binds[bi].HasPad)
                    items.Add(new Item { Kind = Kind.BindPad, R = new Rectangle(colPad, y, 206, RowH - 4), Index = bi });
            }
            int by = pr.Bottom - 50;
            items.Add(new Item { Kind = Kind.PanelButton, R = new Rectangle(pr.X + 24, by, 230, 34), Index = 0, Id = "Restaurar por defecto" });
            items.Add(new Item { Kind = Kind.PanelButton, R = new Rectangle(pr.Right - 174, by, 150, 34), Index = 1, Id = "Hecho" });
            items.Add(new Item { Kind = Kind.Close, R = new Rectangle(W - 46, 12, 34, 30) });
        }

        public void Render(Graphics g)
        {
            BuildItems();
            if (focus >= items.Count) focus = 0;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Ink);

            if (background != null)                          // a toda la ventana, recortando
            {
                float s = Math.Max((float)W / background.Width, (float)H / background.Height);
                float bw = background.Width * s, bh = background.Height * s;
                g.DrawImage(background, (W - bw) / 2, (H - bh) / 2, bw, bh);
            }
            using (var vign = new LinearGradientBrush(new Rectangle(0, H - 150, W, 150), Color.FromArgb(0, 0, 0, 0), Color.FromArgb(170, 6, 4, 4), LinearGradientMode.Vertical))
                g.FillRectangle(vign, 0, H - 149, W, 149);

            // la columna de la izquierda
            using (var side = new LinearGradientBrush(new Rectangle(0, 0, SideW + 60, H), Color.FromArgb(236, 12, 9, 9), Color.FromArgb(0, 12, 9, 9), LinearGradientMode.Horizontal))
            {
                var blend = new ColorBlend(3);
                blend.Colors = new[] { Color.FromArgb(236, 12, 9, 9), Color.FromArgb(222, 12, 9, 9), Color.FromArgb(0, 12, 9, 9) };
                blend.Positions = new[] { 0f, 0.86f, 1f };
                side.InterpolationColors = blend;
                g.FillRectangle(side, 0, 0, SideW + 60, H);
            }
            using (var edge = new Pen(Color.FromArgb(120, Gold), 1f))
            {
                g.DrawLine(edge, SideW + 2, 24, SideW + 2, H - 24);
                g.FillEllipse(new SolidBrush(Gold), SideW - 1, H / 2 - 3, 6, 6);
            }
            using (var frame = new Pen(Color.FromArgb(110, Gold), 1f))
            {
                g.DrawRectangle(frame, 8, 8, W - 17, H - 17);
                DrawCorner(g, frame, 8, 8, 1, 1); DrawCorner(g, frame, W - 9, 8, -1, 1);
                DrawCorner(g, frame, 8, H - 9, 1, -1); DrawCorner(g, frame, W - 9, H - 9, -1, -1);
            }

            if (logo != null)
            {
                float lw = SideW - 2 * PadX + 20, s = Math.Min(lw / logo.Width, (float)LogoMaxH / logo.Height);
                g.DrawImage(logo, (SideW - logo.Width * s) / 2, LogoTop, logo.Width * s, logo.Height * s);
            }
            else DrawTitle(g, "Pandora's Tower", PadX, 50);

            if (panel) DrawPanel(g);
            if (!panel && infoY >= 0)
            {
                string dir = GameDir();
                string shown = isoPath.Length > 0 ? isoPath : dir.Length > 0 ? dir + L.T("  (carpeta)") : L.T("sin elegir: elige la imagen del juego");
                using (var lb = new SolidBrush(Color.FromArgb(196, 194, 204)))
                    g.DrawString(L.T("Imagen del juego"), labelFont, lb, PadX, infoY);
                using (var vb = new SolidBrush(dir.Length > 0 ? Ivory : Gold))
                using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisPath, FormatFlags = StringFormatFlags.NoWrap })
                    g.DrawString(shown, smallFont, vb, new RectangleF(PadX, infoY + 28, SideW - 2 * PadX, 20), sf);
            }
            for (int i = 0; i < items.Count; i++) DrawItem(g, items[i], i == focus, i == hover);

            using (var hb = new SolidBrush(Color.FromArgb(180, Muted)))
            {
                if (panel)
                {
                    Rectangle pr = PanelRect;
                    string hint = L.T("Clic: asignar · clic derecho: borrar (en el mando, cambiar) · rueda o ↑↓: más filas · Esc: volver");
                    g.DrawString(status.Length > 0 ? status : hint, hintFont, hb, new RectangleF(pr.X + 24, pr.Bottom - 84, pr.Width - 48, 30));
                }
                else
                {
                    string hint = L.T("↑↓ elegir · ←→ cambiar · Esc: salir");
                    g.DrawString(status.Length > 0 ? status : hint, hintFont, hb, new RectangleF(PadX + 110, H - 44, SideW - PadX - 110 + (status.Length > 0 ? 600 : 0), 22));
                }
            }
        }

        void DrawItem(Graphics g, Item it, bool sel, bool hot)
        {
            switch (it.Kind)
            {
                case Kind.Tab:
                {
                    bool cur = it.Index == page;
                    using (var b = new SolidBrush(cur ? Gold : (sel || hot ? Silver : Muted)))
                        g.DrawString(L.T(Pages[it.Index]), tabFont, b, it.R.X + 4, it.R.Y + 2);
                    if (cur) using (var p = new Pen(Gold, 1.5f)) g.DrawLine(p, it.R.X + 4, it.R.Bottom - 2, it.R.Right - 8, it.R.Bottom - 2);
                    else if (sel) using (var p = new Pen(Color.FromArgb(120, Silver), 1f)) g.DrawLine(p, it.R.X + 4, it.R.Bottom - 2, it.R.Right - 8, it.R.Bottom - 2);
                    break;
                }
                case Kind.Option:
                {
                    Option o = optionById[it.Id];
                    Rectangle r = it.R;
                    if (sel || hot)
                        using (var band = new LinearGradientBrush(r, Color.FromArgb(sel ? 70 : 38, Gold), Color.FromArgb(0, Gold), LinearGradientMode.Horizontal))
                            g.FillRectangle(band, r);
                    if (sel) using (var gb = new SolidBrush(Gold)) g.DrawString("✦", glyphFont, gb, PadX - 22, r.Y + 6);
                    using (var lb = new SolidBrush(sel ? Silver : Color.FromArgb(196, 194, 204)))
                        g.DrawString(L.T(o.Label), labelFont, lb, PadX, r.Y + 2);
                    using (var ab = new SolidBrush(sel || hot ? Gold : Color.FromArgb(120, Gold)))
                    {
                        g.DrawString("‹", valueFont, ab, PadX, r.Y + 29);
                        g.DrawString("›", valueFont, ab, SideW - 30, r.Y + 29);
                    }
                    using (var vb = new SolidBrush(sel ? Gold : Ivory))
                    {
                        string shown = o.Choices[o.Index].Display();
                        g.DrawString(shown, FontFor(valueFont, shown), vb, new RectangleF(PadX + 16, r.Y + 30, SideW - PadX - 52, 20));
                    }
                    break;
                }
                case Kind.Action:
                case Kind.PanelButton:
                    DrawButton(g, it.R, L.T(it.Id), buttonFont, false, sel || hot);
                    break;
                case Kind.Play:
                    DrawButton(g, it.R, L.T("Jugar"), playFont, true, sel || hot);
                    break;
                case Kind.Exit:
                    using (var b = new SolidBrush(sel || hot ? Gold : Muted)) g.DrawString(L.T("Salir"), buttonFont, b, it.R.X, it.R.Y);
                    break;
                case Kind.Close:
                    using (var cb = new SolidBrush(sel || hot ? Gold : Muted)) g.DrawString("✕", glyphFont, cb, it.R.X + 9, it.R.Y + 5);
                    break;
                case Kind.BindKeys:
                case Kind.BindPad:
                {
                    Bind b = CurrentBinds[it.Index];
                    bool capturing = capture == b && captureIsPad == (it.Kind == Kind.BindPad);
                    using (var path = Rounded(it.R, 4))
                    {
                        using (var fill = new SolidBrush(capturing ? Color.FromArgb(120, 120, 88, 40) : (sel || hot ? Color.FromArgb(70, Gold) : Color.FromArgb(70, 0, 0, 0))))
                            g.FillPath(fill, path);
                        using (var pen = new Pen(capturing || sel || hot ? Gold : Color.FromArgb(90, Gold), 1f)) g.DrawPath(pen, path);
                    }
                    string text = capturing ? L.T(captureIsPad ? "Pulsa un botón del mando…" : "Pulsa una tecla o botón del ratón…")
                                            : (it.Kind == Kind.BindPad ? L.T(PadLabel(b.Pad)) : KeysLabel(NonMouse(b.Keys)));
                    float tx = it.R.X + 8;
                    if (!capturing && it.Kind == Kind.BindKeys)          // los botones del ratón, con su icono
                        foreach (string k in b.Keys)
                        {
                            Image mi = MouseIcon(k);
                            if (mi == null) continue;
                            int sz = it.R.Height - 4;
                            g.DrawImage(mi, tx - 3, it.R.Y + 2, sz, sz);
                            tx += sz - 2;
                        }
                    if (!capturing && it.Kind == Kind.BindKeys && text == "—" && tx > it.R.X + 8) text = "";
                    using (var tb = new SolidBrush(capturing ? Gold : Ivory))
                    using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                        g.DrawString(text, smallFont, tb, new RectangleF(tx, it.R.Y + 5, it.R.Right - 4 - tx, it.R.Height - 6), sf);
                    break;
                }
            }
        }

        void DrawPanel(Graphics g)
        {
            Rectangle pr = PanelRect;
            using (var path = Rounded(pr, 8))
            {
                using (var fill = new SolidBrush(Color.FromArgb(232, 16, 12, 12))) g.FillPath(fill, path);
                using (var pen = new Pen(Color.FromArgb(170, Gold), 1.2f)) g.DrawPath(pen, path);
            }
            List<Bind> binds = CurrentBinds;
            bool pad = binds == classicBinds;
            using (var tb = new SolidBrush(Silver))
                g.DrawString(L.T(pad ? "Botones · Mando (Classic Controller)" : "Teclas · Ratón y teclado (Wii Remote + Nunchuk)"), tabFont, tb, pr.X + 24, pr.Y + 16);
            int colKeys = pr.X + 210, colPad = pr.X + pr.Width - 230;
            using (var hb = new SolidBrush(GoldDim))
            {
                g.DrawString(L.T("Botón del juego"), smallFont, hb, pr.X + 24, pr.Y + 62);
                g.DrawString(L.T(pad ? "Teclado y ratón (respaldo)" : "Teclado y ratón (el ratón es el puntero)"), smallFont, hb, colKeys, pr.Y + 62);
                if (pad) g.DrawString(L.T("Mando"), smallFont, hb, colPad, pr.Y + 62);
            }
            int n = Math.Min(VisibleRows, binds.Count - panelScroll);
            using (var lb = new SolidBrush(Ivory))
                for (int i = 0; i < n; i++)
                    g.DrawString(L.T(binds[panelScroll + i].Label), labelFont, lb, pr.X + 24, pr.Y + PanelHead + i * RowH - 1);
            if (binds.Count > VisibleRows)
                using (var sb = new SolidBrush(Muted))
                    g.DrawString(string.Format(L.T("{0}–{1} de {2}"), panelScroll + 1, panelScroll + n, binds.Count), hintFont, sb, pr.Right - 110, pr.Y + 24);
        }

        void DrawButton(Graphics g, Rectangle r, string text, Font f, bool primary, bool lit)
        {
            using (var path = Rounded(r, primary ? 6 : 4))
            {
                if (primary)
                    using (var fill = new LinearGradientBrush(r, Color.FromArgb(lit ? 255 : 225, 128, 94, 42), Color.FromArgb(lit ? 255 : 225, 58, 38, 16), LinearGradientMode.Vertical))
                        g.FillPath(fill, path);
                else if (lit)
                    using (var fill = new SolidBrush(Color.FromArgb(60, Gold))) g.FillPath(fill, path);
                using (var pen = new Pen(lit ? Gold : GoldDim, primary ? 1.6f : 1f)) g.DrawPath(pen, path);
            }
            SizeF sz = g.MeasureString(text, f);
            using (var tb = new SolidBrush(primary ? Color.FromArgb(252, 242, 214) : (lit ? Gold : Silver)))
                g.DrawString(text, f, tb, r.X + (r.Width - sz.Width) / 2, r.Y + (r.Height - sz.Height) / 2 + 1);
        }

        // letras plateadas con borde oscuro y sombra (si no hay logo en imagen)
        void DrawTitle(Graphics g, string text, float x, float y)
        {
            using (var path = new GraphicsPath())
            {
                path.AddString(text, gothic, (int)FontStyle.Regular, titleFont.Size, new PointF(x, y), StringFormat.GenericTypographic);
                RectangleF b = path.GetBounds();
                using (var edge = new Pen(Color.FromArgb(235, 28, 24, 30), 5f) { LineJoin = LineJoin.Round }) g.DrawPath(edge, path);
                using (var fill = new LinearGradientBrush(new RectangleF(b.X, b.Y - 1, b.Width, b.Height + 2), Color.White, Color.White, LinearGradientMode.Vertical))
                {
                    var blend = new ColorBlend(4);
                    blend.Colors = new[] { Color.FromArgb(252, 252, 255), Color.FromArgb(196, 198, 210), Color.FromArgb(150, 152, 166), Color.FromArgb(240, 241, 247) };
                    blend.Positions = new[] { 0f, 0.42f, 0.62f, 1f };
                    fill.InterpolationColors = blend;
                    g.FillPath(fill, path);
                }
            }
        }

        static void DrawCorner(Graphics g, Pen p, int x, int y, int dx, int dy)
        {
            g.DrawLine(p, x + dx * 4, y + dy * 4, x + dx * 34, y + dy * 4);
            g.DrawLine(p, x + dx * 4, y + dy * 4, x + dx * 4, y + dy * 34);
            g.DrawEllipse(p, x + dx * 8 - 2, y + dy * 8 - 2, 4, 4);
        }

        static GraphicsPath Rounded(Rectangle r, int rad)
        {
            var p = new GraphicsPath();
            int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void OnPaint(PaintEventArgs e) { Render(e.Graphics); }

        // ---- ratón ----------------------------------------------------------------------------
        int HitTest(Point p)
        {
            for (int i = 0; i < items.Count; i++) if (items[i].R.Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging) { Location = new Point(Location.X + e.X - dragFrom.X, Location.Y + e.Y - dragFrom.Y); return; }
            int h = HitTest(e.Location);
            if (h != hover) { hover = h; Cursor = h < 0 ? Cursors.Default : Cursors.Hand; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { dragging = false; }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (panel) { ScrollPanel(e.Delta > 0 ? -3 : 3); Invalidate(); }
        }

        void ScrollPanel(int d)
        {
            int max = Math.Max(0, CurrentBinds.Count - VisibleRows);
            panelScroll = Math.Max(0, Math.Min(max, panelScroll + d));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (capture != null && !captureIsPad)                      // un botón del ratón como tecla
            {
                string name = e.Button == MouseButtons.Left ? "Mouse Left" : e.Button == MouseButtons.Right ? "Mouse Right"
                            : e.Button == MouseButtons.Middle ? "Mouse Middle" : e.Button == MouseButtons.XButton1 ? "Mouse X1"
                            : e.Button == MouseButtons.XButton2 ? "Mouse X2" : null;
                if (name != null) FinishKey(name);
                return;
            }
            if (capture != null) { capture = null; Invalidate(); return; }
            int h = HitTest(e.Location);
            if (h < 0) { dragging = e.Button == MouseButtons.Left; dragFrom = e.Location; return; }
            focus = h;
            Item it = items[h];
            if (it.Kind == Kind.Option)
            {
                int dir = e.X < PadX + 40 ? -1 : 1;
                if (e.Button == MouseButtons.Right) dir = -1;
                Change(it, dir);
            }
            else if ((it.Kind == Kind.BindKeys || it.Kind == Kind.BindPad) && e.Button == MouseButtons.Right)
            {
                Bind b = CurrentBinds[it.Index];
                if (it.Kind == Kind.BindKeys) { b.Keys.Clear(); SaveControls(); }
                else { b.Pad = PadCycle[(Array.IndexOf(PadCycle, b.Pad ?? "") + 1) % PadCycle.Length]; SaveControls(); }
            }
            else Press(it);
            Invalidate();
        }

        // ---- teclado ----------------------------------------------------------------------------
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (capture != null)
            {
                if (key == Keys.Escape) { capture = null; status = ""; Invalidate(); return true; }
                if (!captureIsPad)
                {
                    if (key == Keys.Delete) { capture.Keys.Clear(); capture = null; SaveControls(); Invalidate(); return true; }
                    string name = SdlKeyName(key);
                    if (name != null) FinishKey(name);
                    else status = L.T("Esa tecla está reservada o no se puede usar.");
                    Invalidate();
                }
                return true;
            }
            switch (key)
            {
                case Keys.Up: MoveFocus(-1); return true;
                case Keys.Down: MoveFocus(1); return true;
                case Keys.Left: ChangeFocused(-1); return true;
                case Keys.Right: ChangeFocused(1); return true;
                case Keys.Enter:
                case Keys.Space:
                    if (focus < items.Count && items[focus].Kind != Kind.Option && items[focus].Kind != Kind.Tab) Press(items[focus]);
                    else if (!panel) Play();
                    Invalidate();
                    return true;
                case Keys.Escape:
                    if (panel) { panel = false; focus = 0; Invalidate(); } else Close();
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void FinishKey(string name)
        {
            capture.Keys.Clear();
            capture.Keys.Add(name);
            capture = null;
            status = "";
            SaveControls();
            Invalidate();
        }

        void MoveFocus(int d)
        {
            if (items.Count == 0) return;
            if (panel)
            {
                Item cur = items[focus];
                bool row = cur.Kind == Kind.BindKeys || cur.Kind == Kind.BindPad;
                if (row)
                {
                    int target = cur.Index + d;
                    if (target < panelScroll && panelScroll > 0) { ScrollPanel(-1); BuildItems(); }
                    else if (target >= panelScroll + VisibleRows && target < CurrentBinds.Count) { ScrollPanel(1); BuildItems(); }
                    for (int i = 0; i < items.Count; i++)
                        if (items[i].Kind == cur.Kind && items[i].Index == target) { focus = i; Invalidate(); return; }
                    if (d > 0) for (int i = 0; i < items.Count; i++) if (items[i].Kind == Kind.PanelButton) { focus = i; Invalidate(); return; }
                }
            }
            focus = (focus + d + items.Count) % items.Count;
            if (items[focus].Kind == Kind.Close) focus = (focus + d + items.Count) % items.Count;
            status = "";
            Invalidate();
        }

        void ChangeFocused(int d)
        {
            if (focus >= items.Count) return;
            Item it = items[focus];
            if (it.Kind == Kind.Option) Change(it, d);
            else if (it.Kind == Kind.Tab) { page = (page + d + Pages.Length) % Pages.Length; focus = page; }
            else if (panel && (it.Kind == Kind.BindKeys || it.Kind == Kind.BindPad))
            {
                Kind other = it.Kind == Kind.BindKeys ? Kind.BindPad : Kind.BindKeys;
                for (int i = 0; i < items.Count; i++) if (items[i].Kind == other && items[i].Index == it.Index) { focus = i; break; }
            }
            else MoveFocus(d);
            Invalidate();
        }

        void Change(Item it, int d)
        {
            optionById[it.Id].Step(d);
            if (it.Id == "idioma") { L.Lang = Get("idioma").Key; SetupFonts(); }
            status = "";
            SaveSettings();
            if (it.Id == "zona") SaveControls();
        }

        // ---- mando --------------------------------------------------------------------------------
        void PollPad()
        {
            if (!ContainsFocus) return;
            XInput.State s;
            if (!XInput.Read(out s)) return;
            ushort b = s.Buttons;
            if (capture != null && captureIsPad)
            {
                if (!captureArmed) { if (b == 0 && s.LeftTrigger < 64 && s.RightTrigger < 64) captureArmed = true; return; }
                string name = null;
                for (int i = 0; i < XInput.Bits.Length; i++) if ((b & XInput.Bits[i]) != 0) name = XInput.Names[i];
                if (s.LeftTrigger > 128) name = "Left Trigger";
                if (s.RightTrigger > 128) name = "Right Trigger";
                if (name != null) { capture.Pad = name; capture = null; SaveControls(); padPrev = b; Invalidate(); }
                return;
            }
            if (s.LY > 16000) b |= 0x0001; else if (s.LY < -16000) b |= 0x0002;
            if (s.LX < -16000) b |= 0x0004; else if (s.LX > 16000) b |= 0x0008;
            ushort pressed = (ushort)(b & ~padPrev);
            if ((b & 0x000F) != 0 && (b & 0x000F) == (padPrev & 0x000F))
            {
                if (++padRepeat > 9) { pressed |= (ushort)(b & 0x000F); padRepeat = 7; }
            }
            else padRepeat = 0;
            padPrev = b;
            if (capture != null) return;
            if ((pressed & 0x0001) != 0) MoveFocus(-1);
            if ((pressed & 0x0002) != 0) MoveFocus(1);
            if ((pressed & 0x0004) != 0) ChangeFocused(-1);
            if ((pressed & 0x0008) != 0) ChangeFocused(1);
            if ((pressed & 0x0100) != 0 && !panel) { page = (page + Pages.Length - 1) % Pages.Length; focus = page; Invalidate(); }   // LB
            if ((pressed & 0x0200) != 0 && !panel) { page = (page + 1) % Pages.Length; focus = page; Invalidate(); }   // RB
            if ((pressed & 0x1000) != 0 && focus < items.Count)                                               // A
            {
                Item it = items[focus];
                if (it.Kind == Kind.Option) Change(it, 1);
                else if (it.Kind == Kind.Tab) { page = it.Index; }
                else Press(it);
                Invalidate();
            }
            if ((pressed & 0x0010) != 0 && !panel) Play();                                                   // Start
            if ((pressed & 0x2000) != 0) { if (panel) { panel = false; focus = 0; Invalidate(); } else Close(); }   // B
        }

        // ---- acciones -------------------------------------------------------------------------
        void Press(Item it)
        {
            switch (it.Kind)
            {
                case Kind.Tab: page = it.Index; focus = it.Index; status = ""; break;
                case Kind.Play: Play(); break;
                case Kind.Exit:
                case Kind.Close: if (panel) { panel = false; focus = 0; } else Close(); break;
                case Kind.Action:
                    if (it.Id == "Asignar botones y teclas") { panel = true; panelScroll = 0; focus = 0; status = ""; }
                    else if (it.Id == "Elegir la imagen del juego") ChooseIso();
                    else if (it.Id == "Copiar partidas") Backup();
                    else if (it.Id == "Abrir partidas")
                    {
                        string nand = Path.Combine(build, "nand");
                        if (Directory.Exists(nand)) Process.Start("explorer.exe", "\"" + nand + "\"");
                        else status = L.T("Todavía no hay partidas guardadas.");
                    }
                    break;
                case Kind.BindKeys:
                case Kind.BindPad:
                    capture = CurrentBinds[it.Index];
                    captureIsPad = it.Kind == Kind.BindPad;
                    captureArmed = false;
                    status = captureIsPad ? L.T("Pulsa un botón del mando (Esc: cancelar). Si tu mando no es XInput, usa el clic derecho para cambiarlo.")
                                          : L.T("Pulsa una tecla o un botón del ratón (Esc: cancelar, Supr: dejar sin tecla).");
                    break;
                case Kind.PanelButton:
                    if (it.Index == 0)
                    {
                        foreach (Bind b in CurrentBinds) { b.Keys.Clear(); b.Keys.AddRange(b.DefaultKeys); b.Pad = b.DefaultPad; }
                        SaveControls();
                        status = L.T("Controles restaurados.");
                    }
                    else { panel = false; focus = 0; status = ""; }
                    break;
            }
        }

        void Backup()
        {
            string nand = Path.Combine(build, "nand");
            if (!Directory.Exists(nand)) { status = L.T("Todavía no hay partidas guardadas."); return; }
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string zip = Path.Combine(docs, "PandorasTower-partidas-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm") + ".zip");
            try
            {
                var psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -Command \"Compress-Archive -Path '" + nand + "\\*' -DestinationPath '" + zip + "' -Force\"");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi)) p.WaitForExit();
                status = File.Exists(zip) ? L.T("Copia guardada en Documentos: ") + Path.GetFileName(zip) : L.T("No se pudo hacer la copia.");
            }
            catch (Exception ex) { status = L.T("No se pudo hacer la copia: ") + ex.Message; }
        }

        static string Q(string s) { return "\"" + s + "\""; }

        int ScaleValue()
        {
            string k = Get("resolucion").Key;
            if (k == "wii") return 1;
            if (k == "pantalla") return Math.Max(1, (int)Math.Round(Screen.PrimaryScreen.Bounds.Height / 528.0));
            return int.Parse(k);
        }

        void Play()
        {
            SaveSettings();
            SaveControls();
            string game = GameDir();
            // la japonesa (SX3J01) tiene su compilación aparte, en build\jp
            string gameBuild = GameId(game) == "SX3J01" ? Path.Combine(build, "jp") : build;
            string exe = Path.Combine(gameBuild, "recomp-build", "wiiboot.exe");
            if (gameBuild != build && !File.Exists(exe)) { status = L.T("La versión japonesa no está compilada: ejecuta ./setup.sh con su ISO."); Invalidate(); return; }
            if (!File.Exists(exe)) { status = L.T("No encuentro el juego compilado: ejecuta ./setup.sh y ./lanzador.sh."); Invalidate(); return; }
            if (game.Length == 0 || !(Directory.Exists(game) || File.Exists(game))) { status = L.T("Elige la imagen del juego en la página Juego."); page = 3; Invalidate(); return; }
            string err = Path.Combine(build, "run.err");
            var args = new List<string>();
            args.Add(Q(game));
            args.Add("--nand"); args.Add(Q(Path.Combine(build, "nand")));
            args.Add("--fonts"); args.Add(Q(Path.Combine(build, "fonts")));
            args.Add("--symbols"); args.Add(Q(Path.Combine(gameBuild, "recomp", "symbols.tsv")));
            args.Add("--keys"); args.Add(Q(ControlsPath));
            args.Add("--language"); args.Add(Get("idioma").Key);
            args.Add("--scale"); args.Add(ScaleValue().ToString());
            if (Get("pantalla").Key != "ventana") args.Add("--fullscreen");

            var psi = new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe");
            // cmd /c ""programa" argumentos 2> "registro"": cmd quita las comillas de fuera
            psi.Arguments = "/c \"" + Q(exe) + " " + string.Join(" ", args.ToArray()) + " 2> " + Q(err) + "\"";
            psi.WorkingDirectory = root;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            foreach (string k in new[] { "PANDORA_FPS", "PANDORA_INPUT", "PANDORA_STRAP", "WIIKIT_ANISO", "WIIKIT_INTERP", "WIIKIT_INTERP_FPS", "WIIKIT_FULLSCREEN", "WIIKIT_SHADER_CACHE" }) psi.EnvironmentVariables.Remove(k);
            if (Get("pantalla").Key == "exclusiva") psi.EnvironmentVariables["WIIKIT_FULLSCREEN"] = "exclusive";
            psi.EnvironmentVariables.Remove("WIIKIT_ASYNC_SHADERS");
            psi.EnvironmentVariables["WIIKIT_ASYNC_SHADERS"] = Get("shaders").Key == "async" ? "1" : "0";
            psi.EnvironmentVariables.Remove("WIIKIT_FIT");
            psi.EnvironmentVariables.Remove("WIIKIT_CROP");
            psi.EnvironmentVariables.Remove("WIIKIT_LETTERBOX");
            string fitKey = Get("bandas").Key;
            if (fitKey == "llenar" || fitKey == "cine") psi.EnvironmentVariables["WIIKIT_FIT"] = "fill";
            if (fitKey == "estirar") psi.EnvironmentVariables["WIIKIT_FIT"] = "stretch";
            psi.EnvironmentVariables.Remove("PANDORA_SIN_BANDAS");
            if (fitKey == "cine")
            {
                psi.EnvironmentVariables["WIIKIT_LETTERBOX"] = "auto";      // las de las películas
                psi.EnvironmentVariables["PANDORA_SIN_BANDAS"] = "1";       // las de las escenas, ni se dibujan
            }
            if (Get("correa").Key == "mostrar") psi.EnvironmentVariables["PANDORA_STRAP"] = "1";
            string fk = Get("fps").Key;
            if (fk == "60n") psi.EnvironmentVariables["PANDORA_FPS"] = "60";   // el juego dibuja a 60
            else if (fk != "30")                                 // el juego a 30, los intermedios interpolados
            {
                psi.EnvironmentVariables["WIIKIT_INTERP"] = "1";
                if (fk != "monitor") psi.EnvironmentVariables["WIIKIT_INTERP_FPS"] = fk;
            }
            psi.EnvironmentVariables["WIIKIT_SHADER_CACHE"] = Path.Combine(build, "shadercache");
            psi.EnvironmentVariables.Remove("WIIKIT_TEXTURE_PACK");
            var packList = new List<string>();                       // el primero que tenga una textura gana
            if (Get("iconos").Key != "wii")
            {
                string pack = ComposeIcons(Get("iconos").Key);
                if (pack != null) packList.Add(pack);
            }
            if (Get("paquete").Key != "ninguno")
            {
                string hd = Path.Combine(root, "texturas", Get("paquete").Key);
                if (Directory.Exists(hd)) packList.Add(hd);
            }
            if (packList.Count > 0) psi.EnvironmentVariables["WIIKIT_TEXTURE_PACK"] = string.Join(";", packList.ToArray());
            if (Get("controles").Key == "raton") psi.EnvironmentVariables["PANDORA_INPUT"] = "remote";
            psi.EnvironmentVariables["WIIKIT_ANISO"] = Get("texturas").Key;

            Process proc;
            try { proc = Process.Start(psi); }
            catch (Exception ex) { status = L.T("No se pudo arrancar el juego: ") + ex.Message; Invalidate(); return; }
            padTimer.Stop();
            Hide();
            proc.WaitForExit();
            if (proc.ExitCode != 0)
            {
                string tail = "";
                try
                {
                    string[] lines = File.ReadAllLines(err);
                    int from = Math.Max(0, lines.Length - 8);
                    tail = string.Join("\n", lines, from, lines.Length - from);
                }
                catch (Exception) { }
                MessageBox.Show(string.Format(L.T("El juego se cerró con el código {0}.\n\nÚltimas líneas de build\\run.err:\n{1}"), proc.ExitCode, tail),
                                "Pandora's Tower", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            Close();
        }

        // para la vista previa (--captura): abrir una página o el panel
// ---- iconos de botones ----------------------------------------------------------------------
        // tools/iconos/texturas.txt dice qué texturas del juego muestran qué botón del Classic
        // Controller; con los iconos de tools/iconos/xbox|ps|teclado y tu asignación de botones
        // (controles.txt) se componen en build/iconos_activos, que wiiboot usa como paquete
        // de texturas (WIIKIT_TEXTURE_PACK). Devuelve esa carpeta, o null si no hay nada.
        public string ComposeIcons(string family)
        {
            string table = Path.Combine(root, "tools", "iconos", "texturas.txt");
            if (!File.Exists(table)) return null;
            // con ratón y teclado se juega con el Wii Remote y el Nunchuk: sus iconos son
            // siempre los del ratón y las teclas que tengan asignados
            if (Get("controles").Key == "raton") family = "teclado";
            string outDir = Path.Combine(build, "iconos_activos");
            try
            {
                Directory.CreateDirectory(outDir);
                foreach (string old in Directory.GetFiles(outDir, "*.png")) File.Delete(old);
            }
            catch (Exception) { return null; }
            int made = 0;
            foreach (string raw in File.ReadAllLines(table, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] f = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (f.Length < 2) continue;
                string name = f[0], button = f[1], mode = f.Length > 2 ? f[2] : "";
                int x = name.IndexOf('x'), u = name.IndexOf('_');
                int tw, th;
                if (x < 0 || u < x || !int.TryParse(name.Substring(0, x), out tw) || !int.TryParse(name.Substring(x + 1, u - x - 1), out th)) continue;
                using (Bitmap glyph = Glyph(family, button))
                {
                    if (glyph == null) continue;
                    const int K = 4;                                   // los iconos, a 4 veces su tamaño
                    using (var bmp = new Bitmap(tw * K, th * K, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        Rectangle area = new Rectangle(0, 0, tw * K, th * K);
                        if (mode == "componer")
                        {
                            // la textura volcada (build/texdump, o build/texdump-raton de
                            // ./run.sh --volcar-iconos), con el icono en su rectángulo
                            string dump = Path.Combine(build, "texdump", name + ".png");
                            if (!File.Exists(dump)) dump = Path.Combine(build, "texdump-raton", name + ".png");
                            int rx, ry, rw, rh;
                            if (f.Length < 7 || !File.Exists(dump) || !int.TryParse(f[3], out rx) || !int.TryParse(f[4], out ry)
                                || !int.TryParse(f[5], out rw) || !int.TryParse(f[6], out rh)) continue;
                            using (Image baseImg = LoadImage(dump))
                            {
                                if (baseImg == null) continue;
                                g.DrawImage(baseImg, area);
                            }
                            area = new Rectangle(rx * K, ry * K, rw * K, rh * K);
                            g.CompositingMode = CompositingMode.SourceCopy;
                            bool black = f.Length > 7 && f[7] == "negro";           // texturas sin transparencia
                            using (var clear = new SolidBrush(black ? Color.Black : Color.Transparent)) g.FillRectangle(clear, area);
                            g.CompositingMode = CompositingMode.SourceOver;
                        }
                        else if (mode == "negro")                         // texturas sin transparencia: fondo negro
                        {
                            using (var black = new SolidBrush(Color.Black)) g.FillRectangle(black, area);
                        }
                        else if (mode == "silueta")
                        {
                            // la máscara de otra textura (blanco lo que se ve, negro lo que no): la
                            // silueta del icono, un poco más gruesa, en blanco sobre negro
                            using (var black = new SolidBrush(Color.Black)) g.FillRectangle(black, area);
                            float s0 = Math.Min((float)area.Width / glyph.Width, (float)area.Height / glyph.Height);
                            using (Bitmap sil = Silhouette(glyph, (int)Math.Round(glyph.Width * s0), (int)Math.Round(glyph.Height * s0), K))
                                g.DrawImage(sil, area.X + (area.Width - sil.Width) / 2, area.Y + (area.Height - sil.Height) / 2, sil.Width, sil.Height);
                            bmp.Save(Path.Combine(outDir, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                            ++made;
                            continue;
                        }
                        else if (mode == "anillo")
                        {
                            Rectangle ring = new Rectangle(area.X + 4, area.Y + 4, area.Width - 8, area.Height - 8);
                            using (var fill = new SolidBrush(Color.FromArgb(225, 18, 16, 20))) g.FillEllipse(fill, ring);
                            using (var pen = new Pen(Color.FromArgb(200, 150, 140, 120), 6f)) g.DrawEllipse(pen, ring);
                            area = new Rectangle(area.X + area.Width * 3 / 20, area.Y + area.Height * 3 / 20, area.Width * 7 / 10, area.Height * 7 / 10);
                        }
                        float sc = Math.Min((float)area.Width / glyph.Width, (float)area.Height / glyph.Height);
                        float gw = glyph.Width * sc, gh = glyph.Height * sc;
                        g.DrawImage(glyph, area.X + (area.Width - gw) / 2, area.Y + (area.Height - gh) / 2, gw, gh);
                        bmp.Save(Path.Combine(outDir, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                        ++made;
                    }
                }
            }
            return made > 0 ? outDir : null;
        }

        // el icono de un botón del Classic en una familia: el botón del mando asignado
        // (xbox, ps) o la tecla (teclado), de controles.txt
        Bitmap Glyph(string family, string button)
        {
            string dir = Path.Combine(root, "tools", "iconos", family);
            // jugando con ratón y teclado, el juego lleva un Wii Remote y un Nunchuk: los iconos que
            // comparte con el Classic (+, −, la cruceta, el stick) son de sus botones, y sus teclas
            if (family == "teclado" && Get("controles").Key == "raton" && !button.StartsWith("W:"))
            {
                switch (button)
                {
                    case "A": case "B": case "Plus": case "Minus": button = "W:" + button; break;
                    case "DpadV": return KeyPairIn(remoteBinds, "Up", "Down");
                    case "DpadH": return KeyPairIn(remoteBinds, "Left", "Right");
                    case "StickL": return StickCap(remoteBinds, "Stick Up", "Stick Left", "Stick Down", "Stick Right");
                    default: return null;                            // X, Y, L, R: no los tiene
                }
            }
            if (button.StartsWith("W:"))                            // un botón del Wii Remote o el Nunchuk
            {
                if (family != "teclado") return null;                // con mando se juega con el Classic
                switch (button)
                {
                    case "W:Pointer":                                // apuntar a la pantalla: el ratón
                        { Image mm = MouseIcon("Mouse Move"); return mm != null ? new Bitmap(mm) : null; }
                    case "W:Shake":                                  // agitar: su tecla o botón, entre arcos de movimiento
                        {
                            if (Get("agitar").Key == "raton")            // o el ratón, agitado
                            { Image ms = MouseIcon("Mouse Shake"); if (ms != null) return new Bitmap(ms); }
                            string sk = KeyOf(remoteBinds, "Shake");
                            if (sk == null) return null;
                            Image mi = MouseIcon(sk);
                            using (Bitmap kb = mi != null ? new Bitmap(mi) : Keycap(CapLabel(sk))) return ShakeCap(kb);
                        }
                    case "W:DpadV": return KeyPairIn(remoteBinds, "Up", "Down");
                    case "W:DpadH": return KeyPairIn(remoteBinds, "Left", "Right");
                    case "W:Dpad": return Cap4(remoteBinds, "Up", "Left", "Down", "Right", "Cruceta");
                    case "W:Stick": return StickCap(remoteBinds, "Stick Up", "Stick Left", "Stick Down", "Stick Right");
                }
                string wk = KeyOf(remoteBinds, button.Substring(2));
                if (wk == null) return null;
                string wpng = Path.Combine(dir, wk + ".png");
                if (File.Exists(wpng)) return LoadImage(wpng) as Bitmap;
                return Keycap(CapLabel(wk));
            }
            if (family == "teclado")
            {
                if (button == "DpadV") return KeyPair("Up", "Down");
                if (button == "DpadH") return KeyPair("Left", "Right");
                if (button == "StickL")
                {
                    string up = FirstKey("Left Stick Up"), left = FirstKey("Left Stick Left"), down = FirstKey("Left Stick Down"), right = FirstKey("Left Stick Right");
                    bool letters = up != null && left != null && down != null && right != null && up.Length == 1 && left.Length == 1 && down.Length == 1 && right.Length == 1;
                    return Keycap(letters ? up + left + down + right : "Stick");
                }
                string k = FirstKey(button);
                if (k == null) return null;
                string png = Path.Combine(dir, k + ".png");
                if (File.Exists(png)) return LoadImage(png) as Bitmap;
                return Keycap(CapLabel(k));
            }
            string pad;
            if (button == "DpadV") pad = PadOf("Up") == "Dpad Up" ? "Dpad Vertical" : PadOf("Up");
            else if (button == "DpadH") pad = PadOf("Left") == "Dpad Left" ? "Dpad Horizontal" : PadOf("Left");
            else if (button == "StickL") pad = "Stick L";
            else pad = PadOf(button);
            if (pad == null) return null;
            string file = Path.Combine(dir, pad + ".png");
            return File.Exists(file) ? LoadImage(file) as Bitmap : null;
        }

readonly Dictionary<string, Image> mouseIcons = new Dictionary<string, Image>();
        Image MouseIcon(string key)
        {
            if (key != "Mouse Left" && key != "Mouse Right" && key != "Mouse Middle" && key != "Mouse Move" && key != "Mouse Shake") return null;
            Image im;
            if (!mouseIcons.TryGetValue(key, out im))
            {
                string png = Path.Combine(root, "tools", "iconos", "teclado", key + ".png");
                im = File.Exists(png) ? LoadImage(png) : null;
                mouseIcons[key] = im;
            }
            return im;
        }

        static List<string> NonMouse(List<string> keys)
        {
            var r = new List<string>();
            foreach (string k in keys) if (k != "Mouse Left" && k != "Mouse Right" && k != "Mouse Middle") r.Add(k);
            return r;
        }

                string PadOf(string id) { Bind b = FindBind(classicBinds, id); return b != null ? b.Pad : null; }
        string FirstKey(string id) { return KeyOf(classicBinds, id); }

        // la tecla que enseña el icono de un botón: un botón del ratón si lo tiene, o su primera tecla.
        // "W:X" es el botón X del Wii Remote y el Nunchuk (esquema de ratón y teclado)
        string KeyOf(List<Bind> binds, string id)
        {
            Bind b = FindBind(binds, id);
            if (b == null || b.Keys.Count == 0) return null;
            foreach (string k in b.Keys) if (k == "Mouse Left" || k == "Mouse Right" || k == "Mouse Middle") return k;
            return b.Keys[0];
        }

Bitmap KeyPairIn(List<Bind> binds, string a, string b)
        {
            string ka = KeyOf(binds, a), kb = KeyOf(binds, b);
            if (ka == null && kb == null) return null;
            return Keycap((ka != null ? CapLabel(ka) : "") + (kb != null ? CapLabel(kb) : ""));
        }

        Bitmap StickCap(List<Bind> binds, string u, string l, string d, string r)
        {
            return Cap4(binds, u, l, d, r, "Stick");
        }

        // cuatro teclas en una (WASD, las flechas): sus letras si son de una sola, o el nombre
        Bitmap Cap4(List<Bind> binds, string u, string l, string d, string r, string fallback)
        {
            string[] ids = { u, l, d, r };
            string text = "";
            foreach (string id in ids)
            {
                string k = KeyOf(binds, id);
                string c = k != null ? CapLabel(k) : null;
                if (c == null || c.Length != 1) return Keycap(L.T(fallback));
                text += c;
            }
            return Keycap(text);
        }

                Bitmap KeyPair(string a, string b)
        {
            string ka = FirstKey(a), kb = FirstKey(b);
            if (ka == null && kb == null) return null;
            return Keycap((ka != null ? CapLabel(ka) : "") + (kb != null ? CapLabel(kb) : ""));
        }

        static string CapLabel(string k) { return L.T(CapLabelEs(k)); }
        static string CapLabelEs(string k)
        {
            switch (k)
            {
                case "Return": case "Keypad Enter": return "Intro";
                case "Space": return "Espacio";
                case "Backspace": return "Retro";
                case "Left Shift": case "Right Shift": return "Mayús";
                case "Left Ctrl": case "Right Ctrl": return "Ctrl";
                case "Left Alt": case "Right Alt": return "Alt";
                case "Up": return "↑";
                case "Down": return "↓";
                case "Left": return "←";
                case "Right": return "→";
                case "Mouse X1": return "X1";
                case "Mouse X2": return "X2";
            }
            if (k.StartsWith("Keypad ")) return "Num" + k.Substring(7);
            return k;
        }

        // la silueta de un icono a w × h, ensanchada r píxeles: blanca (opaca) donde el icono
        // se ve, transparente donde no
        static Bitmap Silhouette(Bitmap glyph, int w, int h, int r)
        {
            var scaled = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(glyph, 0, 0, w, h);
            }
            var rect = new Rectangle(0, 0, w, h);
            var px = new int[w * h];
            var data = scaled.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Marshal.Copy(data.Scan0, px, 0, px.Length);
            scaled.UnlockBits(data);
            scaled.Dispose();
            var a = new byte[w * h];
            for (int i = 0; i < a.Length; i++) a[i] = (byte)((px[i] >> 24) & 0xFF);
            var o = new int[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int m = 0;
                    for (int dy = -r; dy <= r && m < 255; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= h) continue;
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int xx = x + dx;
                            if (xx < 0 || xx >= w || dx * dx + dy * dy > r * r) continue;
                            if (a[yy * w + xx] > m) m = a[yy * w + xx];
                        }
                    }
                    o[y * w + x] = (m << 24) | 0xFFFFFF;
                }
            var outBmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            data = outBmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Marshal.Copy(o, 0, data.Scan0, o.Length);
            outBmp.UnlockBits(data);
            return outBmp;
        }

        // agitar el Wii Remote: el icono de su tecla o botón del ratón, más pequeño, entre
        // dos pares de arcos de movimiento (del color de los botones del ratón)
        Bitmap ShakeCap(Image key)
        {
            const int S = 128;
            var bmp = new Bitmap(S, S, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                const float box = 74;
                float sc = Math.Min(box / key.Width, box / key.Height);
                float w = key.Width * sc, h = key.Height * sc;
                g.DrawImage(key, (S - w) / 2, (S - h) / 2, w, h);
                using (var dark = new Pen(Color.FromArgb(220, 30, 22, 10), 10f))
                using (var hi = new Pen(Color.FromArgb(255, 225, 160, 70), 6f))
                {
                    dark.StartCap = dark.EndCap = hi.StartCap = hi.EndCap = LineCap.Round;
                    foreach (float r in new[] { 45f, 57f })
                    {
                        var rc = new RectangleF(S / 2f - r, S / 2f - r, 2 * r, 2 * r);
                        foreach (Pen p in new[] { dark, hi })
                        {
                            g.DrawArc(p, rc, 145, 70);                       // a la izquierda
                            g.DrawArc(p, rc, -35, 70);                       // a la derecha
                        }
                    }
                }
            }
            return bmp;
        }

        // una tecla: gris oscuro con el borde claro, como los demás iconos
        Bitmap Keycap(string label)
        {
            const int S = 128;
            var bmp = new Bitmap(S, S, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                Rectangle r = new Rectangle(8, 16, S - 16, S - 30);
                using (var sh = Rounded(new Rectangle(r.X + 2, r.Y + 6, r.Width, r.Height), 18))
                using (var shb = new SolidBrush(Color.FromArgb(140, 0, 0, 0))) g.FillPath(shb, sh);
                using (var path = Rounded(r, 18))
                {
                    using (var fill = new LinearGradientBrush(r, Color.FromArgb(255, 64, 66, 76), Color.FromArgb(255, 30, 31, 37), LinearGradientMode.Vertical))
                        g.FillPath(fill, path);
                    using (var pen = new Pen(Color.FromArgb(255, 170, 172, 182), 4f)) g.DrawPath(pen, path);
                }
                string fam = PickFont("Segoe UI Semibold", "Segoe UI", "DejaVu Sans");
                float size = 54;
                SizeF sz;
                Font font = null;
                while (true)
                {
                    if (font != null) font.Dispose();
                    font = new Font(fam, size, FontStyle.Bold, GraphicsUnit.Pixel);
                    sz = g.MeasureString(label, font);
                    if ((sz.Width <= r.Width - 14 && sz.Height <= r.Height) || size <= 16) break;
                    size -= 2;
                }
                using (var tb = new SolidBrush(Color.FromArgb(240, 240, 245)))
                    g.DrawString(label, font, tb, r.X + (r.Width - sz.Width) / 2, r.Y + (r.Height - sz.Height) / 2);
                font.Dispose();
            }
            return bmp;
        }

        public void UseScheme(string key) { Option o; if (optionById.TryGetValue("controles", out o)) o.Select(key); }

                public void Preview(int pg, bool withPanel) { page = pg; panel = withPanel; focus = withPanel ? 1 : pg; }
    }

    public static class Program
    {
        [STAThread]
        public static void Main(string[] argv)
        {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            Run(root.TrimEnd('\\', '/'), argv);
        }

        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        // Lanzador.ps1 llama aquí con la carpeta del port. Con "--captura FICHERO.png [página] [panel]"
        // dibuja la ventana en un PNG y sale (para ver el diseño sin abrirla).
        public static void Run(string root, string[] argv)
        {
            // píxeles reales de la pantalla (para "Nativa de tu pantalla") aunque
            // Windows tenga el escalado al 125 % o al 150 %
            try { SetProcessDPIAware(); } catch (Exception) { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var form = new LauncherForm(root))
            {
                if (argv != null && argv.Length >= 2 && argv[0] == "--componer-iconos")
                {
                    // --componer-iconos FAMILIA [mando|raton]: el esquema, si no el del lanzador
                    if (argv.Length >= 3) form.UseScheme(argv[2]);
                    string dir = argv[1] == "wii" ? null : form.ComposeIcons(argv[1]);
                    Console.WriteLine(dir ?? "");
                    return;
                }
                if (argv != null && argv.Length >= 2 && argv[0] == "--captura")
                {
                    form.Preview(argv.Length > 2 ? int.Parse(argv[2]) : 0, argv.Length > 3 && argv[3] == "panel");
                    using (var bmp = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
                    {
                        using (Graphics g = Graphics.FromImage(bmp)) form.Render(g);
                        bmp.Save(argv[1], System.Drawing.Imaging.ImageFormat.Png);
                    }
                    return;
                }
                Application.Run(form);
            }
        }
    }
}
