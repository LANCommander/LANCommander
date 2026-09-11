/*
 * sqlite3_os_dos.c -- a SQLite VFS for MS-DOS, built on ANSI stdio.
 *
 * SQLite's own Unix VFS does not build for DJGPP. Its __DJGPP__ shim is
 * stale (osFstat is still declared with the three-argument signature it had
 * before the call sites changed), and the surrounding code wants ETIMEDOUT,
 * nanosleep, statfs and fcntl byte-range locking, none of which DJGPP has.
 * Rather than carry a patch against a 250k-line vendored amalgamation --
 * which the next re-vendoring would silently drop -- the launcher builds
 * SQLite with SQLITE_OS_OTHER=1 and supplies this.
 *
 * It is smaller than the Unix VFS rather than a cut-down copy of it, because
 * most of what that file does is not applicable here:
 *
 *   * Locking is a genuine no-op. DOS is single-tasking and the launcher is
 *     the only process running; there is no second connection to exclude.
 *     This is the honest implementation, not a stub -- and it is exactly
 *     what SQLite's own documentation prescribes for such a platform.
 *
 *   * There is no fsync. INT 21h/68h flushes a handle's buffers, which DJGPP
 *     exposes as fsync(), and that is what xSync calls after flushing stdio.
 *
 *   * Offsets are long, so a database is limited to 2 GB. DOS filesystems
 *     cap a file at 2 GB anyway (4 GB on FAT32, but the DOS API is signed),
 *     so nothing is lost that the platform offered.
 *
 * Everything here is C89 for the same reason the rest of the vintage code is.
 */

#include "sqlite3.h"

#ifdef SQLITE_OS_OTHER

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include <errno.h>
#include <unistd.h>
#include <sys/stat.h>

#define DOS_MAX_PATH 260

typedef struct DosFile DosFile;
struct DosFile {
    sqlite3_file base;      /* must be first */
    FILE        *fp;
    char        *delete_on_close;   /* owned; set for temp files */
};

/* ------------------------------------------------------------------ */
/* sqlite3_file methods                                               */
/* ------------------------------------------------------------------ */

static int dosClose(sqlite3_file *pFile)
{
    DosFile *p = (DosFile *)pFile;

    if (p->fp != NULL) {
        fclose(p->fp);
        p->fp = NULL;
    }

    if (p->delete_on_close != NULL) {
        remove(p->delete_on_close);
        sqlite3_free(p->delete_on_close);
        p->delete_on_close = NULL;
    }

    return SQLITE_OK;
}

static int dosRead(sqlite3_file *pFile, void *zBuf, int iAmt,
                   sqlite3_int64 iOfst)
{
    DosFile *p = (DosFile *)pFile;
    size_t got;

    if (fseek(p->fp, (long)iOfst, SEEK_SET) != 0)
        return SQLITE_IOERR_READ;

    got = fread(zBuf, 1, (size_t)iAmt, p->fp);

    if (got == (size_t)iAmt)
        return SQLITE_OK;

    /* A short read past end-of-file is not an error to SQLite, but the tail
     * of the buffer has to be zeroed -- it reads page headers out of it. */
    memset((char *)zBuf + got, 0, (size_t)iAmt - got);

    return SQLITE_IOERR_SHORT_READ;
}

static int dosWrite(sqlite3_file *pFile, const void *zBuf, int iAmt,
                    sqlite3_int64 iOfst)
{
    DosFile *p = (DosFile *)pFile;

    if (fseek(p->fp, (long)iOfst, SEEK_SET) != 0)
        return SQLITE_IOERR_WRITE;

    if (fwrite(zBuf, 1, (size_t)iAmt, p->fp) != (size_t)iAmt)
        return SQLITE_IOERR_WRITE;

    return SQLITE_OK;
}

static int dosTruncate(sqlite3_file *pFile, sqlite3_int64 size)
{
    DosFile *p = (DosFile *)pFile;

    /* DJGPP has ftruncate; it is INT 21h/4200h + a zero-length write, which
     * is the only way DOS shortens a file. */
    if (fflush(p->fp) != 0)
        return SQLITE_IOERR_TRUNCATE;

    if (ftruncate(fileno(p->fp), (off_t)size) != 0)
        return SQLITE_IOERR_TRUNCATE;

    return SQLITE_OK;
}

static int dosSync(sqlite3_file *pFile, int flags)
{
    DosFile *p = (DosFile *)pFile;

    (void)flags;    /* DOS has no barrier/full distinction to honour */

    if (fflush(p->fp) != 0)
        return SQLITE_IOERR_FSYNC;

    /* Pushes DOS's own buffers out to the disk. Ignored if the DOS version
     * does not implement it, which is the same guarantee a 1990s machine
     * gave any other program. */
    fsync(fileno(p->fp));

    return SQLITE_OK;
}

static int dosFileSize(sqlite3_file *pFile, sqlite3_int64 *pSize)
{
    DosFile *p = (DosFile *)pFile;
    long here, end;

    if (fflush(p->fp) != 0)
        return SQLITE_IOERR_FSTAT;

    here = ftell(p->fp);
    if (here < 0 || fseek(p->fp, 0, SEEK_END) != 0)
        return SQLITE_IOERR_FSTAT;

    end = ftell(p->fp);
    fseek(p->fp, here, SEEK_SET);

    if (end < 0)
        return SQLITE_IOERR_FSTAT;

    *pSize = (sqlite3_int64)end;

    return SQLITE_OK;
}

/* Locking. See the file header: one process, so every lock succeeds and no
 * lock is ever contended. */
static int dosLock(sqlite3_file *pFile, int eLock)
{
    (void)pFile; (void)eLock;
    return SQLITE_OK;
}

static int dosUnlock(sqlite3_file *pFile, int eLock)
{
    (void)pFile; (void)eLock;
    return SQLITE_OK;
}

static int dosCheckReservedLock(sqlite3_file *pFile, int *pResOut)
{
    (void)pFile;
    *pResOut = 0;
    return SQLITE_OK;
}

static int dosFileControl(sqlite3_file *pFile, int op, void *pArg)
{
    (void)pFile; (void)pArg;

    if (op == SQLITE_FCNTL_VFSNAME) {
        *(char **)pArg = sqlite3_mprintf("dos");
        return SQLITE_OK;
    }

    return SQLITE_NOTFOUND;
}

static int dosSectorSize(sqlite3_file *pFile)
{
    (void)pFile;
    return 512;
}

static int dosDeviceCharacteristics(sqlite3_file *pFile)
{
    (void)pFile;
    return 0;
}

static const sqlite3_io_methods dosIoMethods = {
    1,                          /* iVersion */
    dosClose,
    dosRead,
    dosWrite,
    dosTruncate,
    dosSync,
    dosFileSize,
    dosLock,
    dosUnlock,
    dosCheckReservedLock,
    dosFileControl,
    dosSectorSize,
    dosDeviceCharacteristics,
    0, 0, 0, 0, 0, 0, 0, 0, 0   /* v2/v3 methods: unused at iVersion 1 */
};

/* ------------------------------------------------------------------ */
/* sqlite3_vfs methods                                                */
/* ------------------------------------------------------------------ */

static void dos_temp_name(char *zBuf, int nBuf)
{
    const char *dir = getenv("TEMP");
    static unsigned counter = 0;
    unsigned seed;

    if (dir == NULL || *dir == '\0')
        dir = getenv("TMP");
    if (dir == NULL || *dir == '\0')
        dir = ".";

    /* 8.3-safe: "SQnnnnnn.TMP". The counter makes two temp files in the same
     * second distinct, which the clock alone would not. */
    seed = (unsigned)time(NULL) + (counter++ * 7919u);
    sqlite3_snprintf(nBuf, zBuf, "%s\\SQ%06u.TMP", dir, seed % 1000000u);
}

static int dosOpen(sqlite3_vfs *pVfs, const char *zName, sqlite3_file *pFile,
                   int flags, int *pOutFlags)
{
    DosFile *p = (DosFile *)pFile;
    char zTmp[DOS_MAX_PATH];
    const char *mode;
    FILE *fp;

    (void)pVfs;

    memset(p, 0, sizeof(*p));

    if (zName == NULL) {
        dos_temp_name(zTmp, (int)sizeof(zTmp));
        zName = zTmp;

        p->delete_on_close = sqlite3_mprintf("%s", zName);
        if (p->delete_on_close == NULL)
            return SQLITE_NOMEM;
    } else if (flags & SQLITE_OPEN_DELETEONCLOSE) {
        p->delete_on_close = sqlite3_mprintf("%s", zName);
        if (p->delete_on_close == NULL)
            return SQLITE_NOMEM;
    }

    if (flags & SQLITE_OPEN_READONLY) {
        mode = "rb";
    } else {
        /* "r+b" first so an existing database keeps its contents; "w+b"
         * only when it is genuinely absent, which is also what makes
         * SQLITE_OPEN_CREATE meaningful. */
        mode = "r+b";
    }

    fp = fopen(zName, mode);

    if (fp == NULL && (flags & SQLITE_OPEN_READONLY) == 0 &&
        (flags & SQLITE_OPEN_CREATE) != 0) {
        fp = fopen(zName, "w+b");
    }

    if (fp == NULL) {
        if (p->delete_on_close != NULL) {
            sqlite3_free(p->delete_on_close);
            p->delete_on_close = NULL;
        }
        return SQLITE_CANTOPEN;
    }

    /* Unbuffered: SQLite does its own page caching, so stdio buffering only
     * adds a second copy of every page and delays writes it believes have
     * already reached the file. */
    setvbuf(fp, NULL, _IONBF, 0);

    p->base.pMethods = &dosIoMethods;
    p->fp = fp;

    if (pOutFlags != NULL) {
        *pOutFlags = (flags & SQLITE_OPEN_READONLY)
                   ? SQLITE_OPEN_READONLY : SQLITE_OPEN_READWRITE;
    }

    return SQLITE_OK;
}

static int dosDelete(sqlite3_vfs *pVfs, const char *zName, int syncDir)
{
    (void)pVfs; (void)syncDir;   /* no directory to sync on DOS */

    if (remove(zName) != 0 && errno != ENOENT)
        return SQLITE_IOERR_DELETE;

    return SQLITE_OK;
}

static int dosAccess(sqlite3_vfs *pVfs, const char *zName, int flags,
                     int *pResOut)
{
    struct stat st;

    (void)pVfs;

    if (stat(zName, &st) != 0) {
        *pResOut = 0;
        return SQLITE_OK;
    }

    if (flags == SQLITE_ACCESS_EXISTS)
        *pResOut = (st.st_size > 0) || S_ISDIR(st.st_mode);
    else
        *pResOut = 1;   /* every file is readable and writable on DOS */

    return SQLITE_OK;
}

static int dosFullPathname(sqlite3_vfs *pVfs, const char *zName, int nOut,
                           char *zOut)
{
    char cwd[DOS_MAX_PATH];

    (void)pVfs;

    /* Already absolute: a drive letter, or rooted on the current drive. */
    if ((zName[0] != '\0' && zName[1] == ':') ||
        zName[0] == '\\' || zName[0] == '/') {
        sqlite3_snprintf(nOut, zOut, "%s", zName);
        return SQLITE_OK;
    }

    if (getcwd(cwd, sizeof(cwd)) == NULL)
        return SQLITE_IOERR;

    sqlite3_snprintf(nOut, zOut, "%s\\%s", cwd, zName);

    return SQLITE_OK;
}

static int dosRandomness(sqlite3_vfs *pVfs, int nByte, char *zOut)
{
    int i;
    unsigned seed;

    (void)pVfs;

    /* No entropy source on DOS. This seeds SQLite's rollback-journal name
     * generation and nothing security-sensitive, so the wall clock plus the
     * process's own elapsed ticks is enough to keep two journals distinct. */
    seed = (unsigned)time(NULL) ^ ((unsigned)clock() << 8);
    srand(seed);

    for (i = 0; i < nByte; i++)
        zOut[i] = (char)(rand() & 0xFF);

    return nByte;
}

static int dosSleep(sqlite3_vfs *pVfs, int microseconds)
{
    (void)pVfs;

    /* usleep() on DJGPP yields the time slice to the DPMI host rather than
     * spinning, which matters under DOSBox and in a Windows DOS box. */
    usleep((unsigned)microseconds);

    return microseconds;
}

static int dosCurrentTime(sqlite3_vfs *pVfs, double *pNow)
{
    (void)pVfs;

    /* Julian day number of the Unix epoch, plus elapsed days. */
    *pNow = 2440587.5 + (double)time(NULL) / 86400.0;

    return SQLITE_OK;
}

static int dosGetLastError(sqlite3_vfs *pVfs, int nBuf, char *zBuf)
{
    (void)pVfs;

    if (nBuf > 0 && zBuf != NULL)
        sqlite3_snprintf(nBuf, zBuf, "%s", strerror(errno));

    return errno;
}

static sqlite3_vfs dosVfs = {
    1,                      /* iVersion */
    sizeof(DosFile),        /* szOsFile */
    DOS_MAX_PATH,           /* mxPathname */
    0,                      /* pNext */
    "dos",                  /* zName */
    0,                      /* pAppData */
    dosOpen,
    dosDelete,
    dosAccess,
    dosFullPathname,
    0,                      /* xDlOpen  -- no shared libraries on DOS */
    0,                      /* xDlError */
    0,                      /* xDlSym */
    0,                      /* xDlClose */
    dosRandomness,
    dosSleep,
    dosCurrentTime,
    dosGetLastError,
    0, 0, 0, 0, 0, 0        /* v2/v3 methods: unused at iVersion 1 */
};

int sqlite3_os_init(void)
{
    return sqlite3_vfs_register(&dosVfs, 1);
}

int sqlite3_os_end(void)
{
    return SQLITE_OK;
}

#endif /* SQLITE_OS_OTHER */
