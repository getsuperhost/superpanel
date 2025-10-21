#include "pch.h"
#include "SystemMonitor.h"
#include <iostream>
#include <vector>
#include <thread>
#include <chrono>
#include <cstring>

#ifdef _WIN32
#include <pdh.h>
#include <psapi.h>
#include <winsock2.h>
#include <ws2tcpip.h>
#pragma comment(lib, "pdh.lib")
#pragma comment(lib, "psapi.lib")
#pragma comment(lib, "ws2_32.lib")
#else
#include <unistd.h>
#include <sys/statvfs.h>
#include <sys/sysinfo.h>
#include <dirent.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#endif

// Global variables for performance monitoring
static PDH_HQUERY cpuQuery;
static PDH_HCOUNTER cpuTotal;
static bool perfCountersInitialized = false;

void InitializePerfCounters() {
    if (!perfCountersInitialized) {
#ifdef _WIN32
        PdhOpenQuery(NULL, NULL, &cpuQuery);
        PdhAddEnglishCounter(cpuQuery, L"\\Processor(_Total)\\% Processor Time", NULL, &cpuTotal);
        PdhCollectQueryData(cpuQuery);
#endif
        perfCountersInitialized = true;
    }
}

extern "C" {

SUPERPANEL_API double GetCpuUsage() {
    InitializePerfCounters();
    
#ifdef _WIN32
    PDH_FMT_COUNTERVALUE counterVal;
    PdhCollectQueryData(cpuQuery);
    PdhGetFormattedCounterValue(cpuTotal, PDH_FMT_DOUBLE, NULL, &counterVal);
    return counterVal.doubleValue;
#else
    // Linux implementation using /proc/stat
    static unsigned long long lastTotalUser = 0, lastTotalUserLow = 0, lastTotalSys = 0, lastTotalIdle = 0;
    
    FILE* file = fopen("/proc/stat", "r");
    if (file == NULL) return 0.0;
    
    unsigned long long totalUser, totalUserLow, totalSys, totalIdle, total;
    fscanf(file, "cpu %llu %llu %llu %llu", &totalUser, &totalUserLow, &totalSys, &totalIdle);
    fclose(file);
    
    if (lastTotalUser == 0) {
        lastTotalUser = totalUser;
        lastTotalUserLow = totalUserLow;
        lastTotalSys = totalSys;
        lastTotalIdle = totalIdle;
        return 0.0;
    }
    
    total = (totalUser - lastTotalUser) + (totalUserLow - lastTotalUserLow) + (totalSys - lastTotalSys);
    double percent = total * 100.0 / (total + (totalIdle - lastTotalIdle));
    
    lastTotalUser = totalUser;
    lastTotalUserLow = totalUserLow;
    lastTotalSys = totalSys;
    lastTotalIdle = totalIdle;
    
    return percent;
#endif
}

SUPERPANEL_API long long GetAvailableMemory() {
#ifdef _WIN32
    MEMORYSTATUSEX statex;
    statex.dwLength = sizeof(statex);
    GlobalMemoryStatusEx(&statex);
    return statex.ullAvailPhys;
#else
    struct sysinfo info;
    sysinfo(&info);
    return info.freeram * info.mem_unit;
#endif
}

SUPERPANEL_API long long GetTotalMemory() {
#ifdef _WIN32
    MEMORYSTATUSEX statex;
    statex.dwLength = sizeof(statex);
    GlobalMemoryStatusEx(&statex);
    return statex.ullTotalPhys;
#else
    struct sysinfo info;
    sysinfo(&info);
    return info.totalram * info.mem_unit;
#endif
}

SUPERPANEL_API int GetProcessCount() {
#ifdef _WIN32
    DWORD processes[1024];
    DWORD bytesReturned;
    if (EnumProcesses(processes, sizeof(processes), &bytesReturned)) {
        return bytesReturned / sizeof(DWORD);
    }
    return 0;
#else
    DIR* proc = opendir("/proc");
    if (proc == NULL) return 0;
    
    int count = 0;
    struct dirent* entry;
    while ((entry = readdir(proc)) != NULL) {
        if (entry->d_type == DT_DIR && strspn(entry->d_name, "0123456789") == strlen(entry->d_name)) {
            count++;
        }
    }
    closedir(proc);
    return count;
#endif
}

SUPERPANEL_API void GetTopProcesses(int* processIds, char** processNames, long long* memoryUsages, int maxCount) {
#ifdef _WIN32
    DWORD processes[1024];
    DWORD bytesReturned;
    if (!EnumProcesses(processes, sizeof(processes), &bytesReturned)) return;
    
    int processCount = bytesReturned / sizeof(DWORD);
    int count = 0;
    
    for (int i = 0; i < processCount && count < maxCount; i++) {
        HANDLE hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, processes[i]);
        if (hProcess != NULL) {
            PROCESS_MEMORY_COUNTERS pmc;
            if (GetProcessMemoryInfo(hProcess, &pmc, sizeof(pmc))) {
                processIds[count] = processes[i];
                memoryUsages[count] = pmc.WorkingSetSize;
                
                char processName[MAX_PATH];
                if (GetModuleBaseNameA(hProcess, NULL, processName, sizeof(processName))) {
                    strcpy(processNames[count], processName);
                } else {
                    strcpy(processNames[count], "Unknown");
                }
                count++;
            }
            CloseHandle(hProcess);
        }
    }
#else
    // Linux implementation would read from /proc/*/stat and /proc/*/status
    // This is a simplified version
    DIR* proc = opendir("/proc");
    if (proc == NULL) return;
    
    int count = 0;
    struct dirent* entry;
    while ((entry = readdir(proc)) != NULL && count < maxCount) {
        if (entry->d_type == DT_DIR && strspn(entry->d_name, "0123456789") == strlen(entry->d_name)) {
            processIds[count] = atoi(entry->d_name);
            strcpy(processNames[count], entry->d_name);
            memoryUsages[count] = 0; // Would need to read from /proc/pid/status
            count++;
        }
    }
    closedir(proc);
#endif
}

SUPERPANEL_API int GetDiskUsage(const char* path, long long* totalSpace, long long* freeSpace) {
#ifdef _WIN32
    ULARGE_INTEGER freeBytesAvailable, totalNumberOfBytes, totalNumberOfFreeBytes;
    if (GetDiskFreeSpaceExA(path, &freeBytesAvailable, &totalNumberOfBytes, &totalNumberOfFreeBytes)) {
        *totalSpace = totalNumberOfBytes.QuadPart;
        *freeSpace = freeBytesAvailable.QuadPart;
        return 1;
    }
    return 0;
#else
    struct statvfs stat;
    if (statvfs(path, &stat) == 0) {
        *totalSpace = stat.f_blocks * stat.f_frsize;
        *freeSpace = stat.f_bavail * stat.f_frsize;
        return 1;
    }
    return 0;
#endif
}

SUPERPANEL_API int ListDirectory(const char* path, char** fileNames, int maxFiles) {
#ifdef _WIN32
    WIN32_FIND_DATAA findFileData;
    HANDLE hFind;
    
    char searchPath[MAX_PATH];
    snprintf(searchPath, sizeof(searchPath), "%s\\*", path);
    
    hFind = FindFirstFileA(searchPath, &findFileData);
    if (hFind == INVALID_HANDLE_VALUE) return 0;
    
    int count = 0;
    do {
        if (strcmp(findFileData.cFileName, ".") != 0 && strcmp(findFileData.cFileName, "..") != 0) {
            if (count < maxFiles) {
                strcpy(fileNames[count], findFileData.cFileName);
                count++;
            }
        }
    } while (FindNextFileA(hFind, &findFileData) != 0 && count < maxFiles);
    
    FindClose(hFind);
    return count;
#else
    DIR* dir = opendir(path);
    if (dir == NULL) return 0;
    
    int count = 0;
    struct dirent* entry;
    while ((entry = readdir(dir)) != NULL && count < maxFiles) {
        if (strcmp(entry->d_name, ".") != 0 && strcmp(entry->d_name, "..") != 0) {
            strcpy(fileNames[count], entry->d_name);
            count++;
        }
    }
    closedir(dir);
    return count;
#endif
}

SUPERPANEL_API int CheckPortStatus(const char* host, int port) {
#ifdef _WIN32
    WSADATA wsaData;
    if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0) return 0;
#endif
    
    int sock = socket(AF_INET, SOCK_STREAM, 0);
    if (sock < 0) return 0;
    
    struct sockaddr_in address;
    address.sin_family = AF_INET;
    address.sin_port = htons(port);
    
#ifdef _WIN32
    address.sin_addr.s_addr = inet_addr(host);
#else
    inet_pton(AF_INET, host, &address.sin_addr);
#endif
    
    int result = connect(sock, (struct sockaddr*)&address, sizeof(address));
    
#ifdef _WIN32
    closesocket(sock);
    WSACleanup();
#else
    close(sock);
#endif
    
    return (result == 0) ? 1 : 0;
}

SUPERPANEL_API int GetNetworkStats(long long* bytesReceived, long long* bytesSent) {
#ifdef _WIN32
    // Windows implementation using SNMP or WMI would be complex
    // This is a simplified placeholder
    *bytesReceived = 0;
    *bytesSent = 0;
    return 1;
#else
    FILE* file = fopen("/proc/net/dev", "r");
    if (file == NULL) return 0;
    
    char line[256];
    *bytesReceived = 0;
    *bytesSent = 0;
    
    // Skip header lines
    fgets(line, sizeof(line), file);
    fgets(line, sizeof(line), file);
    
    while (fgets(line, sizeof(line), file)) {
        char interface[32];
        long long rx_bytes, tx_bytes;
        if (sscanf(line, "%31[^:]: %lld %*d %*d %*d %*d %*d %*d %*d %lld", interface, &rx_bytes, &tx_bytes) == 3) {
            if (strcmp(interface, "lo") != 0) { // Skip loopback
                *bytesReceived += rx_bytes;
                *bytesSent += tx_bytes;
            }
        }
    }
    fclose(file);
    return 1;
#endif
}

} // extern "C"