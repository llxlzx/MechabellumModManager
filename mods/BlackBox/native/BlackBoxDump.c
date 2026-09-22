#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <dbghelp.h>

#pragma comment(lib, "dbghelp.lib")

static int fail(int code, HANDLE process, HANDLE file, LPWSTR *argv)
{
    if (file && file != INVALID_HANDLE_VALUE) CloseHandle(file);
    if (process) CloseHandle(process);
    if (argv) LocalFree(argv);
    return code;
}

int wmain(void)
{
    int argc = 0;
    LPWSTR *argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (!argv) return 1;

    DWORD pid = 0;
    unsigned long long started = 0;
    const wchar_t *outPath = NULL;
    for (int i = 1; i < argc; i++)
    {
        if (wcscmp(argv[i], L"--pid") == 0 && i + 1 < argc) pid = (DWORD)_wtoi(argv[++i]);
        else if (wcscmp(argv[i], L"--started") == 0 && i + 1 < argc) started = _wcstoui64(argv[++i], NULL, 10);
        else if (wcscmp(argv[i], L"--out") == 0 && i + 1 < argc) outPath = argv[++i];
        else return fail(1, NULL, NULL, argv);
    }
    if (pid == 0 || started == 0 || outPath == NULL || outPath[0] == 0)
        return fail(1, NULL, NULL, argv);

    HANDLE process = OpenProcess(
        PROCESS_QUERY_INFORMATION | PROCESS_VM_READ | PROCESS_DUP_HANDLE, FALSE, pid);
    if (!process) return fail(2, NULL, NULL, argv);

    FILETIME createTime, exitTime, kernel, user;
    if (!GetProcessTimes(process, &createTime, &exitTime, &kernel, &user))
        return fail(2, process, NULL, argv);
    ULARGE_INTEGER got;
    got.LowPart = createTime.dwLowDateTime;
    got.HighPart = createTime.dwHighDateTime;
    if (got.QuadPart != started) return fail(2, process, NULL, argv);

    HANDLE file = CreateFileW(outPath, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (file == INVALID_HANDLE_VALUE) return fail(3, process, NULL, argv);

    MINIDUMP_TYPE flags = MiniDumpNormal | MiniDumpWithThreadInfo | MiniDumpWithUnloadedModules;
    BOOL ok = MiniDumpWriteDump(process, pid, file, flags, NULL, NULL, NULL);
    CloseHandle(file);
    CloseHandle(process);
    LocalFree(argv);
    if (!ok)
    {
        DeleteFileW(outPath);
        return 3;
    }
    return 0;
}
