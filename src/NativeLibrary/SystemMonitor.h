#pragma once

#ifdef SUPERPANELNATIVELIBRARY_EXPORTS
#define SUPERPANEL_API __declspec(dllexport)
#else
#define SUPERPANEL_API __declspec(dllimport)
#endif

extern "C" {
    // System monitoring functions
    SUPERPANEL_API double GetCpuUsage();
    SUPERPANEL_API long long GetAvailableMemory();
    SUPERPANEL_API long long GetTotalMemory();
    SUPERPANEL_API int GetProcessCount();
    SUPERPANEL_API void GetTopProcesses(int* processIds, char** processNames, long long* memoryUsages, int maxCount);
    
    // File system operations
    SUPERPANEL_API int GetDiskUsage(const char* path, long long* totalSpace, long long* freeSpace);
    SUPERPANEL_API int ListDirectory(const char* path, char** fileNames, int maxFiles);
    
    // Network operations
    SUPERPANEL_API int CheckPortStatus(const char* host, int port);
    SUPERPANEL_API int GetNetworkStats(long long* bytesReceived, long long* bytesSent);
}