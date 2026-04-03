#include <windows.h>
#include <iostream>

int main() {
    OSVERSIONINFO osvi;
    ZeroMemory(&osvi, sizeof(OSVERSIONINFO));
    osvi.dwOSVersionInfoSize = sizeof(OSVERSIONINFO);
    GetVersionEx(&osvi);

    if (osvi.dwMajorVersion == 5) {
        // Ez XP (5.1) vagy Win2003 (5.2)
        WinExec("RTS_Legacy_XP.exe", SW_SHOW);
    } else if (osvi.dwMajorVersion == 6) {
        // Ez Vista vagy Win7
        WinExec("RTS_NetFramework48.exe", SW_SHOW);
    } else {
        // Modern Win10 / Win11
        WinExec("RTS_Modern_Net9.exe", SW_SHOW);
    }
    return 0;
}
