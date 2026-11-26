/*
 * kbdusbd.c — "US with Taka sign" Windows Keyboard Layout DLL
 * Maintained by ekushey.org
 *
 * Identical to standard US QWERTY (00000409), except:
 *   - Shift + 4          -> U+09F3 (৳ BENGALI RUPEE SIGN)
 *   - AltGr (Ctrl+Alt)+4 -> U+0024 ($ DOLLAR SIGN)
 */

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#if defined(__has_include)
#  if __has_include(<kbd.h>)
#    include <kbd.h>
#    define HAVE_WDK_KBD_H 1
#  endif
#endif

#ifndef HAVE_WDK_KBD_H

#define KBD_VERSION 1
#define KLLF_ALTGR  0x00000001

#define CAPLOK      0x01
#define SGCAPS      0x02
#define CAPLOKALTGR 0x04
#define KANALOK     0x08
#define GRPSELTAP   0x10

#define WCH_NONE    0xF000
#define WCH_DEAD    0xF001
#define WCH_LGTR    0xF002

#define KBDBASE     0
#define KBDSHIFT    1
#define KBDCTRL     2
#define KBDALT      4
#define KBDKANA     8

#define SHFT_INVALID 0x0F

#define KBDEXT      0x0100
#define KBDMULTIVK  0x0200
#define KBDSPECIAL  0x0400
#define KBDNUMPAD   0x0800
#define KBDUNICODE  0x1000
#define KBDINJECTEDVK 0x2000
#define KBDMAPPEDVK 0x4000
#define KBDBREAK    0x8000

#define VK_ABNT_C1  0xC1
#define VK_ABNT_C2  0xC2

#if defined(BUILD_WOW6432) || defined(_WIN64)
#define KBD_LONG_POINTER __ptr64
#else
#define KBD_LONG_POINTER
#endif

typedef struct _VK_TO_BIT {
    BYTE Vk;
    BYTE ModBits;
} VK_TO_BIT, *KBD_LONG_POINTER PVK_TO_BIT;

typedef struct _MODIFIERS {
    PVK_TO_BIT pVkToBit;
    WORD       wMaxModBits;
    BYTE       ModNumber[];
} MODIFIERS, *KBD_LONG_POINTER PMODIFIERS;

#define TYPEDEF_VK_TO_WCHARS(n) typedef struct _VK_TO_WCHARS##n { \
    BYTE  VirtualKey; \
    BYTE  Attributes; \
    WCHAR wch[n]; \
} VK_TO_WCHARS##n, *KBD_LONG_POINTER PVK_TO_WCHARS##n;

TYPEDEF_VK_TO_WCHARS(1)
TYPEDEF_VK_TO_WCHARS(2)
TYPEDEF_VK_TO_WCHARS(3)
TYPEDEF_VK_TO_WCHARS(4)
TYPEDEF_VK_TO_WCHARS(5)

typedef struct _VK_TO_WCHAR_TABLE {
    PVK_TO_WCHARS1 pVkToWchars;
    BYTE           nModifications;
    BYTE           cbSize;
} VK_TO_WCHAR_TABLE, *KBD_LONG_POINTER PVK_TO_WCHAR_TABLE;

typedef struct _DEADKEY {
    DWORD  dwBoth;
    WCHAR  wchComposed;
    USHORT uFlags;
} DEADKEY, *KBD_LONG_POINTER PDEADKEY;

typedef struct _VSC_LPWSTR {
    BYTE   vsc;
    WCHAR *KBD_LONG_POINTER pwsz;
} VSC_LPWSTR, *KBD_LONG_POINTER PVSC_LPWSTR;

typedef struct _VSC_VK {
    BYTE   Vsc;
    USHORT Vk;
} VSC_VK, *KBD_LONG_POINTER PVSC_VK;

typedef struct _KBDTABLES {
    PMODIFIERS          pCharModifiers;
    PVK_TO_WCHAR_TABLE  pVkToWcharTable;
    PDEADKEY            pDeadKey;
    PVSC_LPWSTR         pKeyNames;
    PVSC_LPWSTR         pKeyNamesExt;
    WCHAR *KBD_LONG_POINTER *KBD_LONG_POINTER pKeyNamesDead;
    USHORT *KBD_LONG_POINTER pusVSCtoVK;
    BYTE                bMaxVSCtoVK;
    PVSC_VK             pVSCtoVK_E0;
    PVSC_VK             pVSCtoVK_E1;
    DWORD               fLocaleFlags;
    BYTE                nLgMax;
    BYTE                cbLgEntry;
    void *KBD_LONG_POINTER pLigature;
    DWORD               dwType;
    DWORD               dwSubType;
} KBDTABLES, *KBD_LONG_POINTER PKBDTABLES;

#endif /* HAVE_WDK_KBD_H */

#pragma data_seg(".data")

/* Modifier bit mapping (includes AltGr = Ctrl + Alt) */
static VK_TO_BIT aVkToBits[] = {
    { VK_SHIFT,   KBDSHIFT },
    { VK_CONTROL, KBDCTRL  },
    { VK_MENU,    KBDALT   },
    { 0,          0        }
};

static MODIFIERS CharModifiers = {
    &aVkToBits[0],
    7,
    {
        0,            /* 000: Base */
        1,            /* 001: Shift */
        2,            /* 010: Ctrl */
        3,            /* 011: Shift + Ctrl */
        SHFT_INVALID, /* 100: Alt */
        SHFT_INVALID, /* 101: Shift + Alt */
        4,            /* 110: AltGr (Ctrl + Alt) */
        SHFT_INVALID  /* 111: Shift + Ctrl + Alt */
    }
};

/* 2-column table: Base, Shift */
static VK_TO_WCHARS2 aVkToWch2[] = {
    { '1',           0,      { '1',  '!'  } },
    { '3',           0,      { '3',  '#'  } },
    { '5',           0,      { '5',  '%'  } },
    { '7',           0,      { '7',  '&'  } },
    { '8',           0,      { '8',  '*'  } },
    { '9',           0,      { '9',  '('  } },
    { '0',           0,      { '0',  ')'  } },
    { VK_OEM_PLUS,   0,      { '=',  '+'  } },
    { 'Q',           CAPLOK, { 'q',  'Q'  } },
    { 'W',           CAPLOK, { 'w',  'W'  } },
    { 'E',           CAPLOK, { 'e',  'E'  } },
    { 'R',           CAPLOK, { 'r',  'R'  } },
    { 'T',           CAPLOK, { 't',  'T'  } },
    { 'Y',           CAPLOK, { 'y',  'Y'  } },
    { 'U',           CAPLOK, { 'u',  'U'  } },
    { 'I',           CAPLOK, { 'i',  'I'  } },
    { 'O',           CAPLOK, { 'o',  'O'  } },
    { 'P',           CAPLOK, { 'p',  'P'  } },
    { 'A',           CAPLOK, { 'a',  'A'  } },
    { 'S',           CAPLOK, { 's',  'S'  } },
    { 'D',           CAPLOK, { 'd',  'D'  } },
    { 'F',           CAPLOK, { 'f',  'F'  } },
    { 'G',           CAPLOK, { 'g',  'G'  } },
    { 'H',           CAPLOK, { 'h',  'H'  } },
    { 'J',           CAPLOK, { 'j',  'J'  } },
    { 'K',           CAPLOK, { 'k',  'K'  } },
    { 'L',           CAPLOK, { 'l',  'L'  } },
    { VK_OEM_1,      0,      { ';',  ':'  } },
    { VK_OEM_7,      0,      { '\'', '"'  } },
    { VK_OEM_3,      0,      { '`',  '~'  } },
    { 'Z',           CAPLOK, { 'z',  'Z'  } },
    { 'X',           CAPLOK, { 'x',  'X'  } },
    { 'C',           CAPLOK, { 'c',  'C'  } },
    { 'V',           CAPLOK, { 'v',  'V'  } },
    { 'B',           CAPLOK, { 'b',  'B'  } },
    { 'N',           CAPLOK, { 'n',  'N'  } },
    { 'M',           CAPLOK, { 'm',  'M'  } },
    { VK_OEM_COMMA,  0,      { ',',  '<'  } },
    { VK_OEM_PERIOD, 0,      { '.',  '>'  } },
    { VK_OEM_2,      0,      { '/',  '?'  } },
    { VK_DECIMAL,    0,      { '.',  '.'  } },
    { VK_TAB,        0,      { '\t', '\t' } },
    { VK_ADD,        0,      { '+',  '+'  } },
    { VK_DIVIDE,     0,      { '/',  '/'  } },
    { VK_MULTIPLY,   0,      { '*',  '*'  } },
    { VK_SUBTRACT,   0,      { '-',  '-'  } },
    { 0,             0,      { 0,    0    } }
};

/* 3-column table: Base, Shift, Ctrl */
static VK_TO_WCHARS3 aVkToWch3[] = {
    { VK_OEM_4,      0, { '[',  '{',  0x001B } },
    { VK_OEM_5,      0, { '\\', '|',  0x001C } },
    { VK_OEM_6,      0, { ']',  '}',  0x001D } },
    { VK_OEM_102,    0, { '\\', '|',  0x001C } },
    { VK_BACK,       0, { '\b', '\b', 0x007F } },
    { VK_ESCAPE,     0, { 0x001B, 0x001B, 0x001B } },
    { VK_RETURN,     0, { '\r', '\r', '\n'   } },
    { VK_SPACE,      0, { ' ',  ' ',  ' '    } },
    { VK_CANCEL,     0, { 0x0003, 0x0003, 0x0003 } },
    { 0,             0, { 0,    0,    0      } }
};

/* 4-column table: Base, Shift, Ctrl, Shift+Ctrl */
static VK_TO_WCHARS4 aVkToWch4[] = {
    { '2',           0, { '2', '@', WCH_NONE, 0x0000 } },
    { '6',           0, { '6', '^', WCH_NONE, 0x001E } },
    { VK_OEM_MINUS,  0, { '-', '_', WCH_NONE, 0x001F } },
    { 0,             0, { 0,   0,   0,        0      } }
};

/* 5-column table: Base, Shift, Ctrl, Shift+Ctrl, AltGr (Ctrl+Alt)
 * Key '4': Base='4', Shift=U+09F3 ('৳'), Ctrl=WCH_NONE, Shift+Ctrl=WCH_NONE, AltGr='$'
 */
static VK_TO_WCHARS5 aVkToWch5[] = {
    { '4',           0, { '4', 0x09F3, WCH_NONE, WCH_NONE, '$' } },
    { 0,             0, { 0,   0,      0,        0,        0   } }
};

/* 1-column keypad digits (NumLock) */
static VK_TO_WCHARS1 aVkToWch1[] = {
    { VK_NUMPAD0, 0, { '0' } },
    { VK_NUMPAD1, 0, { '1' } },
    { VK_NUMPAD2, 0, { '2' } },
    { VK_NUMPAD3, 0, { '3' } },
    { VK_NUMPAD4, 0, { '4' } },
    { VK_NUMPAD5, 0, { '5' } },
    { VK_NUMPAD6, 0, { '6' } },
    { VK_NUMPAD7, 0, { '7' } },
    { VK_NUMPAD8, 0, { '8' } },
    { VK_NUMPAD9, 0, { '9' } },
    { 0,          0, { 0   } }
};

static VK_TO_WCHAR_TABLE aVkToWcharTable[] = {
    { (PVK_TO_WCHARS1)aVkToWch3, 3, sizeof(aVkToWch3[0]) },
    { (PVK_TO_WCHARS1)aVkToWch4, 4, sizeof(aVkToWch4[0]) },
    { (PVK_TO_WCHARS1)aVkToWch5, 5, sizeof(aVkToWch5[0]) },
    { (PVK_TO_WCHARS1)aVkToWch2, 2, sizeof(aVkToWch2[0]) },
    { (PVK_TO_WCHARS1)aVkToWch1, 1, sizeof(aVkToWch1[0]) },
    { NULL,                      0, 0                    }
};

/* Standard 101/102-key US Scan Code -> Virtual Key table */
static USHORT ausVSCtoVK[] = {
    /* 00 */ 0xFF,
    /* 01 */ VK_ESCAPE,
    /* 02 */ '1',
    /* 03 */ '2',
    /* 04 */ '3',
    /* 05 */ '4',
    /* 06 */ '5',
    /* 07 */ '6',
    /* 08 */ '7',
    /* 09 */ '8',
    /* 0A */ '9',
    /* 0B */ '0',
    /* 0C */ VK_OEM_MINUS,
    /* 0D */ VK_OEM_PLUS,
    /* 0E */ VK_BACK,
    /* 0F */ VK_TAB,
    /* 10 */ 'Q',
    /* 11 */ 'W',
    /* 12 */ 'E',
    /* 13 */ 'R',
    /* 14 */ 'T',
    /* 15 */ 'Y',
    /* 16 */ 'U',
    /* 17 */ 'I',
    /* 18 */ 'O',
    /* 19 */ 'P',
    /* 1A */ VK_OEM_4,
    /* 1B */ VK_OEM_6,
    /* 1C */ VK_RETURN,
    /* 1D */ VK_LCONTROL,
    /* 1E */ 'A',
    /* 1F */ 'S',
    /* 20 */ 'D',
    /* 21 */ 'F',
    /* 22 */ 'G',
    /* 23 */ 'H',
    /* 24 */ 'J',
    /* 25 */ 'K',
    /* 26 */ 'L',
    /* 27 */ VK_OEM_1,
    /* 28 */ VK_OEM_7,
    /* 29 */ VK_OEM_3,
    /* 2A */ VK_LSHIFT,
    /* 2B */ VK_OEM_5,
    /* 2C */ 'Z',
    /* 2D */ 'X',
    /* 2E */ 'C',
    /* 2F */ 'V',
    /* 30 */ 'B',
    /* 31 */ 'N',
    /* 32 */ 'M',
    /* 33 */ VK_OEM_COMMA,
    /* 34 */ VK_OEM_PERIOD,
    /* 35 */ VK_OEM_2,
    /* 36 */ VK_RSHIFT | KBDEXT,
    /* 37 */ VK_MULTIPLY | KBDMULTIVK,
    /* 38 */ VK_LMENU,
    /* 39 */ VK_SPACE,
    /* 3A */ VK_CAPITAL,
    /* 3B */ VK_F1,
    /* 3C */ VK_F2,
    /* 3D */ VK_F3,
    /* 3E */ VK_F4,
    /* 3F */ VK_F5,
    /* 40 */ VK_F6,
    /* 41 */ VK_F7,
    /* 42 */ VK_F8,
    /* 43 */ VK_F9,
    /* 44 */ VK_F10,
    /* 45 */ VK_NUMLOCK | KBDEXT | KBDMULTIVK,
    /* 46 */ VK_SCROLL | KBDMULTIVK,
    /* 47 */ VK_HOME | KBDNUMPAD | KBDSPECIAL,
    /* 48 */ VK_UP | KBDNUMPAD | KBDSPECIAL,
    /* 49 */ VK_PRIOR | KBDNUMPAD | KBDSPECIAL,
    /* 4A */ VK_SUBTRACT,
    /* 4B */ VK_LEFT | KBDNUMPAD | KBDSPECIAL,
    /* 4C */ VK_CLEAR | KBDNUMPAD | KBDSPECIAL,
    /* 4D */ VK_RIGHT | KBDNUMPAD | KBDSPECIAL,
    /* 4E */ VK_ADD,
    /* 4F */ VK_END | KBDNUMPAD | KBDSPECIAL,
    /* 50 */ VK_DOWN | KBDNUMPAD | KBDSPECIAL,
    /* 51 */ VK_NEXT | KBDNUMPAD | KBDSPECIAL,
    /* 52 */ VK_INSERT | KBDNUMPAD | KBDSPECIAL,
    /* 53 */ VK_DELETE | KBDNUMPAD | KBDSPECIAL,
    /* 54 */ VK_SNAPSHOT,
    /* 55 */ 0xFF,
    /* 56 */ VK_OEM_102,
    /* 57 */ VK_F11,
    /* 58 */ VK_F12,
    /* 59 */ VK_CLEAR,
    /* 5A */ VK_OEM_WSCTRL,
    /* 5B */ VK_OEM_FINISH,
    /* 5C */ VK_OEM_JUMP,
    /* 5D */ VK_EREOF,
    /* 5E */ VK_OEM_BACKTAB,
    /* 5F */ VK_OEM_AUTO,
    /* 60 */ 0xFF,
    /* 61 */ 0xFF,
    /* 62 */ VK_ZOOM,
    /* 63 */ VK_HELP,
    /* 64 */ VK_F13,
    /* 65 */ VK_F14,
    /* 66 */ VK_F15,
    /* 67 */ VK_F16,
    /* 68 */ VK_F17,
    /* 69 */ VK_F18,
    /* 6A */ VK_F19,
    /* 6B */ VK_F20,
    /* 6C */ VK_F21,
    /* 6D */ VK_F22,
    /* 6E */ VK_F23,
    /* 6F */ VK_OEM_PA3,
    /* 70 */ 0xFF,
    /* 71 */ VK_OEM_RESET,
    /* 72 */ 0xFF,
    /* 73 */ VK_ABNT_C1,
    /* 74 */ 0xFF,
    /* 75 */ 0xFF,
    /* 76 */ VK_F24,
    /* 77 */ 0xFF,
    /* 78 */ 0xFF,
    /* 79 */ 0xFF,
    /* 7A */ 0xFF,
    /* 7B */ VK_OEM_PA1,
    /* 7C */ VK_TAB,
    /* 7D */ 0xFF,
    /* 7E */ VK_ABNT_C2,
    /* 7F */ VK_OEM_PA2
};

static VSC_VK aE0VscToVk[] = {
    { 0x10, VK_MEDIA_PREV_TRACK | KBDEXT },
    { 0x19, VK_MEDIA_NEXT_TRACK | KBDEXT },
    { 0x1C, VK_RETURN           | KBDEXT },
    { 0x1D, VK_RCONTROL         | KBDEXT },
    { 0x20, VK_VOLUME_MUTE      | KBDEXT },
    { 0x21, VK_LAUNCH_APP2      | KBDEXT },
    { 0x22, VK_MEDIA_PLAY_PAUSE | KBDEXT },
    { 0x24, VK_MEDIA_STOP       | KBDEXT },
    { 0x2E, VK_VOLUME_DOWN      | KBDEXT },
    { 0x30, VK_VOLUME_UP        | KBDEXT },
    { 0x32, VK_BROWSER_HOME     | KBDEXT },
    { 0x35, VK_DIVIDE           | KBDEXT },
    { 0x37, VK_SNAPSHOT         | KBDEXT },
    { 0x38, VK_RMENU            | KBDEXT },
    { 0x46, VK_CANCEL           | KBDEXT },
    { 0x47, VK_HOME             | KBDEXT },
    { 0x48, VK_UP               | KBDEXT },
    { 0x49, VK_PRIOR            | KBDEXT },
    { 0x4B, VK_LEFT             | KBDEXT },
    { 0x4D, VK_RIGHT            | KBDEXT },
    { 0x4F, VK_END              | KBDEXT },
    { 0x50, VK_DOWN             | KBDEXT },
    { 0x51, VK_NEXT             | KBDEXT },
    { 0x52, VK_INSERT           | KBDEXT },
    { 0x53, VK_DELETE           | KBDEXT },
    { 0x5B, VK_LWIN             | KBDEXT },
    { 0x5C, VK_RWIN             | KBDEXT },
    { 0x5D, VK_APPS             | KBDEXT },
    { 0x5F, VK_SLEEP            | KBDEXT },
    { 0x65, VK_BROWSER_SEARCH   | KBDEXT },
    { 0x66, VK_BROWSER_FAVORITES| KBDEXT },
    { 0x67, VK_BROWSER_REFRESH  | KBDEXT },
    { 0x68, VK_BROWSER_STOP     | KBDEXT },
    { 0x69, VK_BROWSER_FORWARD  | KBDEXT },
    { 0x6A, VK_BROWSER_BACK     | KBDEXT },
    { 0x6B, VK_LAUNCH_APP1      | KBDEXT },
    { 0x6C, VK_LAUNCH_MAIL      | KBDEXT },
    { 0x6D, VK_LAUNCH_MEDIA_SELECT | KBDEXT },
    { 0,    0 }
};

static VSC_VK aE1VscToVk[] = {
    { 0x1D, VK_PAUSE },
    { 0,    0 }
};

static VSC_LPWSTR aKeyNames[] = {
    { 0x01, L"Esc" },
    { 0x0E, L"Backspace" },
    { 0x0F, L"Tab" },
    { 0x1C, L"Enter" },
    { 0x1D, L"Ctrl" },
    { 0x2A, L"Shift" },
    { 0x36, L"Right Shift" },
    { 0x37, L"Num *" },
    { 0x38, L"Alt" },
    { 0x39, L"Space" },
    { 0x3A, L"Caps Lock" },
    { 0x3B, L"F1" },
    { 0x3C, L"F2" },
    { 0x3D, L"F3" },
    { 0x3E, L"F4" },
    { 0x3F, L"F5" },
    { 0x40, L"F6" },
    { 0x41, L"F7" },
    { 0x42, L"F8" },
    { 0x43, L"F9" },
    { 0x44, L"F10" },
    { 0x45, L"Pause" },
    { 0x46, L"Scroll Lock" },
    { 0x47, L"Num 7" },
    { 0x48, L"Num 8" },
    { 0x49, L"Num 9" },
    { 0x4A, L"Num -" },
    { 0x4B, L"Num 4" },
    { 0x4C, L"Num 5" },
    { 0x4D, L"Num 6" },
    { 0x4E, L"Num +" },
    { 0x4F, L"Num 1" },
    { 0x50, L"Num 2" },
    { 0x51, L"Num 3" },
    { 0x52, L"Num 0" },
    { 0x53, L"Num Del" },
    { 0x54, L"Sys Req" },
    { 0x57, L"F11" },
    { 0x58, L"F12" },
    { 0x7C, L"F13" },
    { 0x7D, L"F14" },
    { 0x7E, L"F15" },
    { 0x7F, L"F16" },
    { 0,    NULL }
};

static VSC_LPWSTR aKeyNamesExt[] = {
    { 0x1C, L"Num Enter" },
    { 0x1D, L"Right Ctrl" },
    { 0x35, L"Num /" },
    { 0x37, L"Prnt Scrn" },
    { 0x38, L"Right Alt" },
    { 0x45, L"Num Lock" },
    { 0x46, L"Break" },
    { 0x47, L"Home" },
    { 0x48, L"Up" },
    { 0x49, L"Page Up" },
    { 0x4B, L"Left" },
    { 0x4D, L"Right" },
    { 0x4F, L"End" },
    { 0x50, L"Down" },
    { 0x51, L"Page Down" },
    { 0x52, L"Insert" },
    { 0x53, L"Delete" },
    { 0x5B, L"Left Windows" },
    { 0x5C, L"Right Windows" },
    { 0x5D, L"Application" },
    { 0,    NULL }
};

static KBDTABLES KbdTables = {
    &CharModifiers,
    aVkToWcharTable,
    NULL,
    aKeyNames,
    aKeyNamesExt,
    NULL,
    ausVSCtoVK,
    sizeof(ausVSCtoVK) / sizeof(ausVSCtoVK[0]),
    aE0VscToVk,
    aE1VscToVk,
    MAKELONG(KLLF_ALTGR, KBD_VERSION),
    0,
    0,
    NULL,
    4,
    0
};

#pragma data_seg()

__declspec(dllexport) PKBDTABLES KbdLayerDescriptor(void)
{
    return &KbdTables;
}
