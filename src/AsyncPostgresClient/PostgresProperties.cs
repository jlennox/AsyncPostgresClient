using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Lennox.AsyncPostgresClient.Pool;

namespace Lennox.AsyncPostgresClient
{
    public struct PostgresPropertySetting
    {
        public string Name { get; }
        public string Value { get; }

        public PostgresPropertySetting(string name, string value) : this()
        {
            Name = name;
            Value = value;
        }

        public PostgresPropertySetting(PostgresPropertyName name, string value)
            : this()
        {
            Name = PostgresProperties.GetPropertyName(name);
            Value = value;
        }

        public static async Task<IReadOnlyList<PostgresPropertySetting>> GetAll(
            bool async,
            PostgresDbConnection connection,
            CancellationToken cancellationToken)
        {
            var properties = new List<PostgresPropertySetting>(240);

            const string sql = "select name, setting from pg_settings";

            using (var command = connection.CreateCommand(sql))
            using (var reader = await command.ExecuteDbDataReader(
                    async, cancellationToken)
                .ConfigureAwait(false) as PostgresDbDataReader)
            {
                while (await reader.Read(async, cancellationToken)
                    .ConfigureAwait(false))
                {
                    properties.Add(new PostgresPropertySetting(
                        reader.GetString(0), reader.GetString(1)));
                }
            }

            return properties;
        }
    }

    // From SHOW ALL on Postgres 9.5.4.
    public static class PostgresProperties
    {
        // Allows modifications of the structure of system tables. (off)
        public const string AllowSystemTableMods = "allow_system_table_mods";
        // Sets the application name to be reported in statistics and logs. (psql)
        public const string ApplicationName = "application_name";
        // Sets the shell command that will be called to archive a WAL file. ((disabled))
        public const string ArchiveCommand = "archive_command";
        // Allows archiving of WAL files using archive_command. (off)
        public const string ArchiveMode = "archive_mode";
        // Forces a switch to the next xlog file if a new file has not been started within N seconds. (0)
        public const string ArchiveTimeout = "archive_timeout";
        // Enable input of NULL elements in arrays. (on)
        public const string ArrayNulls = "array_nulls";
        // Sets the maximum allowed time to complete client authentication. (1min)
        public const string AuthenticationTimeout = "authentication_timeout";
        // Starts the autovacuum subprocess. (on)
        public const string Autovacuum = "autovacuum";
        // Number of tuple inserts, updates, or deletes prior to analyze as a fraction of reltuples. (0.1)
        public const string AutovacuumAnalyzeScaleFactor = "autovacuum_analyze_scale_factor";
        // Minimum number of tuple inserts, updates, or deletes prior to analyze. (50)
        public const string AutovacuumAnalyzeThreshold = "autovacuum_analyze_threshold";
        // Age at which to autovacuum a table to prevent transaction ID wraparound. (200000000)
        public const string AutovacuumFreezeMaxAge = "autovacuum_freeze_max_age";
        // Sets the maximum number of simultaneously running autovacuum worker processes. (3)
        public const string AutovacuumMaxWorkers = "autovacuum_max_workers";
        // Multixact age at which to autovacuum a table to prevent multixact wraparound. (400000000)
        public const string AutovacuumMultixactFreezeMaxAge = "autovacuum_multixact_freeze_max_age";
        // Time to sleep between autovacuum runs. (1min)
        public const string AutovacuumNaptime = "autovacuum_naptime";
        // Vacuum cost delay in milliseconds, for autovacuum. (20ms)
        public const string AutovacuumVacuumCostDelay = "autovacuum_vacuum_cost_delay";
        // Vacuum cost amount available before napping, for autovacuum. (-1)
        public const string AutovacuumVacuumCostLimit = "autovacuum_vacuum_cost_limit";
        // Number of tuple updates or deletes prior to vacuum as a fraction of reltuples. (0.2)
        public const string AutovacuumVacuumScaleFactor = "autovacuum_vacuum_scale_factor";
        // Minimum number of tuple updates or deletes prior to vacuum. (50)
        public const string AutovacuumVacuumThreshold = "autovacuum_vacuum_threshold";
        // Sets the maximum memory to be used by each autovacuum worker process. (-1)
        public const string AutovacuumWorkMem = "autovacuum_work_mem";
        // Sets whether "\'" is allowed in string literals. (safe_encoding)
        public const string BackslashQuote = "backslash_quote";
        // Background writer sleep time between rounds. (200ms)
        public const string BgwriterDelay = "bgwriter_delay";
        // Background writer maximum number of LRU pages to flush per round. (100)
        public const string BgwriterLruMaxpages = "bgwriter_lru_maxpages";
        // Multiple of the average buffer usage to free per round. (2)
        public const string BgwriterLruMultiplier = "bgwriter_lru_multiplier";
        // Shows the size of a disk block. (8192)
        public const string BlockSize = "block_size";
        // Enables advertising the server via Bonjour. (off)
        public const string Bonjour = "bonjour";
        // Sets the Bonjour service name. ()
        public const string BonjourName = "bonjour_name";
        // Sets the output format for bytea. (hex)
        public const string ByteaOutput = "bytea_output";
        // Check function bodies during CREATE FUNCTION. (on)
        public const string CheckFunctionBodies = "check_function_bodies";
        // Time spent flushing dirty buffers during checkpoint, as fraction of checkpoint interval. (0.9)
        public const string CheckpointCompletionTarget = "checkpoint_completion_target";
        // Sets the maximum time between automatic WAL checkpoints. (5min)
        public const string CheckpointTimeout = "checkpoint_timeout";
        // Enables warnings if checkpoint segments are filled more frequently than this. (30s)
        public const string CheckpointWarning = "checkpoint_warning";
        // Sets the client's character set encoding. (UTF8)
        public const string ClientEncoding = "client_encoding";
        // Sets the message levels that are sent to the client. (notice)
        public const string ClientMinMessages = "client_min_messages";
        // Sets the name of the cluster, which is included in the process title. ()
        public const string ClusterName = "cluster_name";
        // Sets the delay in microseconds between transaction commit and flushing WAL to disk. (0)
        public const string CommitDelay = "commit_delay";
        // Sets the minimum concurrent open transactions before performing commit_delay. (5)
        public const string CommitSiblings = "commit_siblings";
        // Enables the planner to use constraints to optimize queries. (partition)
        public const string ConstraintExclusion = "constraint_exclusion";
        // Sets the planner's estimate of the cost of processing each index entry during an index scan. (0.005)
        public const string CpuIndexTupleCost = "cpu_index_tuple_cost";
        // Sets the planner's estimate of the cost of processing each operator or function call. (0.0025)
        public const string CpuOperatorCost = "cpu_operator_cost";
        // Sets the planner's estimate of the cost of processing each tuple (row). (0.01)
        public const string CpuTupleCost = "cpu_tuple_cost";
        // Sets the planner's estimate of the fraction of a cursor's rows that will be retrieved. (0.1)
        public const string CursorTupleFraction = "cursor_tuple_fraction";
        // Shows whether data checksums are turned on for this cluster. (off)
        public const string DataChecksums = "data_checksums";
        // Sets the display format for date and time values. (ISO, MDY)
        public const string DateStyle = "DateStyle";
        // Enables per-database user names. (off)
        public const string DbUserNamespace = "db_user_namespace";
        // Sets the time to wait on a lock before checking for deadlock. (1s)
        public const string DeadlockTimeout = "deadlock_timeout";
        // Shows whether the running server has assertion checks enabled. (off)
        public const string DebugAssertions = "debug_assertions";
        // Indents parse and plan tree displays. (on)
        public const string DebugPrettyPrint = "debug_pretty_print";
        // Logs each query's parse tree. (off)
        public const string DebugPrintParse = "debug_print_parse";
        // Logs each query's execution plan. (off)
        public const string DebugPrintPlan = "debug_print_plan";
        // Logs each query's rewritten parse tree. (off)
        public const string DebugPrintRewritten = "debug_print_rewritten";
        // Sets the default statistics target. (100)
        public const string DefaultStatisticsTarget = "default_statistics_target";
        // Sets the default tablespace to create tables and indexes in. ()
        public const string DefaultTablespace = "default_tablespace";
        // Sets default text search configuration. (pg_catalog.english)
        public const string DefaultTextSearchConfig = "default_text_search_config";
        // Sets the default deferrable status of new transactions. (off)
        public const string DefaultTransactionDeferrable = "default_transaction_deferrable";
        // Sets the transaction isolation level of each new transaction. (read committed)
        public const string DefaultTransactionIsolation = "default_transaction_isolation";
        // Sets the default read-only status of new transactions. (off)
        public const string DefaultTransactionReadOnly = "default_transaction_read_only";
        // Create new tables with OIDs by default. (off)
        public const string DefaultWithOids = "default_with_oids";
        // Selects the dynamic shared memory implementation used. (posix)
        public const string DynamicSharedMemoryType = "dynamic_shared_memory_type";
        // Sets the planner's assumption about the size of the disk cache. (10GB)
        public const string EffectiveCacheSize = "effective_cache_size";
        // Number of simultaneous requests that can be handled efficiently by the disk subsystem. (0)
        public const string EffectiveIoConcurrency = "effective_io_concurrency";
        // Enables the planner's use of bitmap-scan plans. (on)
        public const string EnableBitmapscan = "enable_bitmapscan";
        // Enables the planner's use of hashed aggregation plans. (on)
        public const string EnableHashagg = "enable_hashagg";
        // Enables the planner's use of hash join plans. (on)
        public const string EnableHashjoin = "enable_hashjoin";
        // Enables the planner's use of index-only-scan plans. (on)
        public const string EnableIndexonlyscan = "enable_indexonlyscan";
        // Enables the planner's use of index-scan plans. (on)
        public const string EnableIndexscan = "enable_indexscan";
        // Enables the planner's use of materialization. (on)
        public const string EnableMaterial = "enable_material";
        // Enables the planner's use of merge join plans. (on)
        public const string EnableMergejoin = "enable_mergejoin";
        // Enables the planner's use of nested-loop join plans. (on)
        public const string EnableNestloop = "enable_nestloop";
        // Enables the planner's use of sequential-scan plans. (on)
        public const string EnableSeqscan = "enable_seqscan";
        // Enables the planner's use of explicit sort steps. (on)
        public const string EnableSort = "enable_sort";
        // Enables the planner's use of TID scan plans. (on)
        public const string EnableTidscan = "enable_tidscan";
        // Warn about backslash escapes in ordinary string literals. (on)
        public const string EscapeStringWarning = "escape_string_warning";
        // Sets the application name used to identify PostgreSQL messages in the event log. (PostgreSQL)
        public const string EventSource = "event_source";
        // Terminate session on any error. (off)
        public const string ExitOnError = "exit_on_error";
        // Sets the number of digits displayed for floating-point values. (0)
        public const string ExtraFloatDigits = "extra_float_digits";
        // Sets the FROM-list size beyond which subqueries are not collapsed. (8)
        public const string FromCollapseLimit = "from_collapse_limit";
        // Forces synchronization of updates to disk. (on)
        public const string Fsync = "fsync";
        // Writes full pages to WAL when first modified after a checkpoint. (on)
        public const string FullPageWrites = "full_page_writes";
        // Enables genetic query optimization. (on)
        public const string Geqo = "geqo";
        // GEQO: effort is used to set the default for other GEQO parameters. (5)
        public const string GeqoEffort = "geqo_effort";
        // GEQO: number of iterations of the algorithm. (0)
        public const string GeqoGenerations = "geqo_generations";
        // GEQO: number of individuals in the population. (0)
        public const string GeqoPoolSize = "geqo_pool_size";
        // GEQO: seed for random path selection. (0)
        public const string GeqoSeed = "geqo_seed";
        // GEQO: selective pressure within the population. (2)
        public const string GeqoSelectionBias = "geqo_selection_bias";
        // Sets the threshold of FROM items beyond which GEQO is used. (12)
        public const string GeqoThreshold = "geqo_threshold";
        // Sets the maximum allowed result for exact search by GIN. (0)
        public const string GinFuzzySearchLimit = "gin_fuzzy_search_limit";
        // Sets the maximum size of the pending list for GIN index. (4MB)
        public const string GinPendingListLimit = "gin_pending_list_limit";
        // Allows connections and queries during recovery. (off)
        public const string HotStandby = "hot_standby";
        // Allows feedback from a hot standby to the primary that will avoid query conflicts. (off)
        public const string HotStandbyFeedback = "hot_standby_feedback";
        // Use of huge pages on Linux. (try)
        public const string HugePages = "huge_pages";
        // Continues processing after a checksum failure. (off)
        public const string IgnoreChecksumFailure = "ignore_checksum_failure";
        // Disables reading from system indexes. (off)
        public const string IgnoreSystemIndexes = "ignore_system_indexes";
        // Datetimes are integer based. (on)
        public const string IntegerDatetimes = "integer_datetimes";
        // Sets the display format for interval values. (postgres)
        public const string IntervalStyle = "IntervalStyle";
        // Sets the FROM-list size beyond which JOIN constructs are not flattened. (8)
        public const string JoinCollapseLimit = "join_collapse_limit";
        // Sets whether Kerberos and GSSAPI user names should be treated as case-insensitive. (off)
        public const string KrbCaseinsUsers = "krb_caseins_users";
        // Shows the collation order locale. (C)
        public const string LcCollate = "lc_collate";
        // Shows the character classification and case conversion locale. (C)
        public const string LcCtype = "lc_ctype";
        // Sets the language in which messages are displayed. (C)
        public const string LcMessages = "lc_messages";
        // Sets the locale for formatting monetary amounts. (C)
        public const string LcMonetary = "lc_monetary";
        // Sets the locale for formatting numbers. (C)
        public const string LcNumeric = "lc_numeric";
        // Sets the locale for formatting date and time values. (C)
        public const string LcTime = "lc_time";
        // Sets the host name or IP address(es) to listen to. (*)
        public const string ListenAddresses = "listen_addresses";
        // Enables backward compatibility mode for privilege checks on large objects. (off)
        public const string LoCompatPrivileges = "lo_compat_privileges";
        // Lists unprivileged shared libraries to preload into each backend. ()
        public const string LocalPreloadLibraries = "local_preload_libraries";
        // Sets the maximum allowed duration of any wait for a lock. (0)
        public const string LockTimeout = "lock_timeout";
        // Sets the minimum execution time above which autovacuum actions will be logged. (-1)
        public const string LogAutovacuumMinDuration = "log_autovacuum_min_duration";
        // Logs each checkpoint. (off)
        public const string LogCheckpoints = "log_checkpoints";
        // Logs each successful connection. (off)
        public const string LogConnections = "log_connections";
        // Sets the destination for server log output. (stderr)
        public const string LogDestination = "log_destination";
        // Logs end of a session, including duration. (off)
        public const string LogDisconnections = "log_disconnections";
        // Logs the duration of each completed SQL statement. (off)
        public const string LogDuration = "log_duration";
        // Sets the verbosity of logged messages. (default)
        public const string LogErrorVerbosity = "log_error_verbosity";
        // Writes executor performance statistics to the server log. (off)
        public const string LogExecutorStats = "log_executor_stats";
        // Sets the file permissions for log files. (0600)
        public const string LogFileMode = "log_file_mode";
        // Logs the host name in the connection logs. (off)
        public const string LogHostname = "log_hostname";
        // Controls information prefixed to each log line. ()
        public const string LogLinePrefix = "log_line_prefix";
        // Logs long lock waits. (off)
        public const string LogLockWaits = "log_lock_waits";
        // Sets the minimum execution time above which statements will be logged. (-1)
        public const string LogMinDurationStatement = "log_min_duration_statement";
        // Causes all statements generating error at or above this level to be logged. (error)
        public const string LogMinErrorStatement = "log_min_error_statement";
        // Sets the message levels that are logged. (warning)
        public const string LogMinMessages = "log_min_messages";
        // Writes parser performance statistics to the server log. (off)
        public const string LogParserStats = "log_parser_stats";
        // Writes planner performance statistics to the server log. (off)
        public const string LogPlannerStats = "log_planner_stats";
        // Logs each replication command. (off)
        public const string LogReplicationCommands = "log_replication_commands";
        // Automatic log file rotation will occur after N minutes. (1d)
        public const string LogRotationAge = "log_rotation_age";
        // Automatic log file rotation will occur after N kilobytes. (10MB)
        public const string LogRotationSize = "log_rotation_size";
        // Sets the type of statements logged. (none)
        public const string LogStatement = "log_statement";
        // Writes cumulative performance statistics to the server log. (off)
        public const string LogStatementStats = "log_statement_stats";
        // Log the use of temporary files larger than this number of kilobytes. (-1)
        public const string LogTempFiles = "log_temp_files";
        // Sets the time zone to use in log messages. (US/Pacific)
        public const string LogTimezone = "log_timezone";
        // Truncate existing log files of same name during log rotation. (off)
        public const string LogTruncateOnRotation = "log_truncate_on_rotation";
        // Start a subprocess to capture stderr output and/or csvlogs into log files. (off)
        public const string LoggingCollector = "logging_collector";
        // Sets the maximum memory to be used for maintenance operations. (1GB)
        public const string MaintenanceWorkMem = "maintenance_work_mem";
        // Sets the maximum number of concurrent connections. (100)
        public const string MaxConnections = "max_connections";
        // Sets the maximum number of simultaneously open files for each server process. (1000)
        public const string MaxFilesPerProcess = "max_files_per_process";
        // Shows the maximum number of function arguments. (100)
        public const string MaxFunctionArgs = "max_function_args";
        // Shows the maximum identifier length. (63)
        public const string MaxIdentifierLength = "max_identifier_length";
        // Shows the maximum number of index keys. (32)
        public const string MaxIndexKeys = "max_index_keys";
        // Sets the maximum number of locks per transaction. (64)
        public const string MaxLocksPerTransaction = "max_locks_per_transaction";
        // Sets the maximum number of predicate locks per transaction. (64)
        public const string MaxPredLocksPerTransaction = "max_pred_locks_per_transaction";
        // Sets the maximum number of simultaneously prepared transactions. (0)
        public const string MaxPreparedTransactions = "max_prepared_transactions";
        // Sets the maximum number of simultaneously defined replication slots. (0)
        public const string MaxReplicationSlots = "max_replication_slots";
        // Sets the maximum stack depth, in kilobytes. (2MB)
        public const string MaxStackDepth = "max_stack_depth";
        // Sets the maximum delay before canceling queries when a hot standby server is processing archived WAL data. (30s)
        public const string MaxStandbyArchiveDelay = "max_standby_archive_delay";
        // Sets the maximum delay before canceling queries when a hot standby server is processing streamed WAL data. (30s)
        public const string MaxStandbyStreamingDelay = "max_standby_streaming_delay";
        // Sets the maximum number of simultaneously running WAL sender processes. (0)
        public const string MaxWalSenders = "max_wal_senders";
        // Sets the WAL size that triggers a checkpoint. (1GB)
        public const string MaxWalSize = "max_wal_size";
        // Maximum number of concurrent worker processes. (8)
        public const string MaxWorkerProcesses = "max_worker_processes";
        // Sets the minimum size to shrink the WAL to. (80MB)
        public const string MinWalSize = "min_wal_size";
        // Emit a warning for constructs that changed meaning since PostgreSQL 9.4. (off)
        public const string OperatorPrecedenceWarning = "operator_precedence_warning";
        // Encrypt passwords. (on)
        public const string PasswordEncryption = "password_encryption";
        // Sets the TCP port the server listens on. (5432)
        public const string Port = "port";
        // Waits N seconds on connection startup after authentication. (0)
        public const string PostAuthDelay = "post_auth_delay";
        // Waits N seconds on connection startup before authentication. (0)
        public const string PreAuthDelay = "pre_auth_delay";
        // When generating SQL fragments, quote all identifiers. (off)
        public const string QuoteAllIdentifiers = "quote_all_identifiers";
        // Sets the planner's estimate of the cost of a nonsequentially fetched disk page. (4)
        public const string RandomPageCost = "random_page_cost";
        // Reinitialize server after backend crash. (on)
        public const string RestartAfterCrash = "restart_after_crash";
        // Enable row security. (on)
        public const string RowSecurity = "row_security";
        // Sets the schema search order for names that are not schema-qualified. ("$user", public)
        public const string SearchPath = "search_path";
        // Shows the number of pages per disk file. (1GB)
        public const string SegmentSize = "segment_size";
        // Sets the planner's estimate of the cost of a sequentially fetched disk page. (1)
        public const string SeqPageCost = "seq_page_cost";
        // Sets the server (database) character set encoding. (UTF8)
        public const string ServerEncoding = "server_encoding";
        // Shows the server version. (9.5.4)
        public const string ServerVersion = "server_version";
        // Shows the server version as an integer. (90504)
        public const string ServerVersionNum = "server_version_num";
        // Sets the session's behavior for triggers and rewrite rules. (origin)
        public const string SessionReplicationRole = "session_replication_role";
        // Sets the number of shared memory buffers used by the server. (3584MB)
        public const string SharedBuffers = "shared_buffers";
        // Causes subtables to be included by default in various commands. (on)
        public const string SqlInheritance = "sql_inheritance";
        // Enables SSL connections. (off)
        public const string Ssl = "ssl";
        // Location of the SSL certificate authority file. ()
        public const string SslCaFile = "ssl_ca_file";
        // Location of the SSL server certificate file. (server.crt)
        public const string SslCertFile = "ssl_cert_file";
        // Location of the SSL certificate revocation list file. ()
        public const string SslCrlFile = "ssl_crl_file";
        // Location of the SSL server private key file. (server.key)
        public const string SslKeyFile = "ssl_key_file";
        // Give priority to server ciphersuite order. (on)
        public const string SslPreferServerCiphers = "ssl_prefer_server_ciphers";
        // Causes '...' strings to treat backslashes literally. (on)
        public const string StandardConformingStrings = "standard_conforming_strings";
        // Sets the maximum allowed duration of any statement. (0)
        public const string StatementTimeout = "statement_timeout";
        // Sets the number of connection slots reserved for superusers. (3)
        public const string SuperuserReservedConnections = "superuser_reserved_connections";
        // Enable synchronized sequential scans. (on)
        public const string SynchronizeSeqscans = "synchronize_seqscans";
        // Sets the current transaction's synchronization level. (on)
        public const string SynchronousCommit = "synchronous_commit";
        // List of names of potential synchronous standbys. ()
        public const string SynchronousStandbyNames = "synchronous_standby_names";
        // Sets the syslog "facility" to be used when syslog enabled. (local0)
        public const string SyslogFacility = "syslog_facility";
        // Sets the program name used to identify PostgreSQL messages in syslog. (postgres)
        public const string SyslogIdent = "syslog_ident";
        // Maximum number of TCP keepalive retransmits. (8)
        public const string TcpKeepalivesCount = "tcp_keepalives_count";
        // Time between issuing TCP keepalives. (7200)
        public const string TcpKeepalivesIdle = "tcp_keepalives_idle";
        // Time between TCP keepalive retransmits. (75)
        public const string TcpKeepalivesInterval = "tcp_keepalives_interval";
        // Sets the maximum number of temporary buffers used by each session. (8MB)
        public const string TempBuffers = "temp_buffers";
        // Limits the total size of all temporary files used by each session. (-1)
        public const string TempFileLimit = "temp_file_limit";
        // Sets the tablespace(s) to use for temporary tables and sort files. ()
        public const string TempTablespaces = "temp_tablespaces";
        // Sets the time zone for displaying and interpreting time stamps. (US/Pacific)
        public const string TimeZone = "TimeZone";
        // Selects a file of time zone abbreviations. (Default)
        public const string TimezoneAbbreviations = "timezone_abbreviations";
        // Generates debugging output for LISTEN and NOTIFY. (off)
        public const string TraceNotify = "trace_notify";
        // Enables logging of recovery-related debugging information. (log)
        public const string TraceRecoveryMessages = "trace_recovery_messages";
        // Emit information about resource usage in sorting. (off)
        public const string TraceSort = "trace_sort";
        // Collects information about executing commands. (on)
        public const string TrackActivities = "track_activities";
        // Sets the size reserved for pg_stat_activity.query, in bytes. (1024)
        public const string TrackActivityQuerySize = "track_activity_query_size";
        // Collects transaction commit time. (off)
        public const string TrackCommitTimestamp = "track_commit_timestamp";
        // Collects statistics on database activity. (on)
        public const string TrackCounts = "track_counts";
        // Collects function-level statistics on database activity. (none)
        public const string TrackFunctions = "track_functions";
        // Collects timing statistics for database I/O activity. (off)
        public const string TrackIoTiming = "track_io_timing";
        // Whether to defer a read-only serializable transaction until it can be executed with no possible serialization failures. (off)
        public const string TransactionDeferrable = "transaction_deferrable";
        // Sets the current transaction's isolation level. (read committed)
        public const string TransactionIsolation = "transaction_isolation";
        // Sets the current transaction's read-only status. (off)
        public const string TransactionReadOnly = "transaction_read_only";
        // Treats "expr=NULL" as "expr IS NULL". (off)
        public const string TransformNullEquals = "transform_null_equals";
        // Sets the owning group of the Unix-domain socket. ()
        public const string UnixSocketGroup = "unix_socket_group";
        // Sets the access permissions of the Unix-domain socket. (0777)
        public const string UnixSocketPermissions = "unix_socket_permissions";
        // Updates the process title to show the active SQL command. (on)
        public const string UpdateProcessTitle = "update_process_title";
        // Vacuum cost delay in milliseconds. (0)
        public const string VacuumCostDelay = "vacuum_cost_delay";
        // Vacuum cost amount available before napping. (200)
        public const string VacuumCostLimit = "vacuum_cost_limit";
        // Vacuum cost for a page dirtied by vacuum. (20)
        public const string VacuumCostPageDirty = "vacuum_cost_page_dirty";
        // Vacuum cost for a page found in the buffer cache. (1)
        public const string VacuumCostPageHit = "vacuum_cost_page_hit";
        // Vacuum cost for a page not found in the buffer cache. (10)
        public const string VacuumCostPageMiss = "vacuum_cost_page_miss";
        // Number of transactions by which VACUUM and HOT cleanup should be deferred, if any. (0)
        public const string VacuumDeferCleanupAge = "vacuum_defer_cleanup_age";
        // Minimum age at which VACUUM should freeze a table row. (50000000)
        public const string VacuumFreezeMinAge = "vacuum_freeze_min_age";
        // Age at which VACUUM should scan whole table to freeze tuples. (150000000)
        public const string VacuumFreezeTableAge = "vacuum_freeze_table_age";
        // Minimum age at which VACUUM should freeze a MultiXactId in a table row. (5000000)
        public const string VacuumMultixactFreezeMinAge = "vacuum_multixact_freeze_min_age";
        // Multixact age at which VACUUM should scan whole table to freeze tuples. (150000000)
        public const string VacuumMultixactFreezeTableAge = "vacuum_multixact_freeze_table_age";
        // Shows the block size in the write ahead log. (8192)
        public const string WalBlockSize = "wal_block_size";
        // Sets the number of disk-page buffers in shared memory for WAL. (16MB)
        public const string WalBuffers = "wal_buffers";
        // Compresses full-page writes written in WAL file. (off)
        public const string WalCompression = "wal_compression";
        // Sets the number of WAL files held for standby servers. (0)
        public const string WalKeepSegments = "wal_keep_segments";
        // Set the level of information written to the WAL. (minimal)
        public const string WalLevel = "wal_level";
        // Writes full pages to WAL when first modified after a checkpoint, even for a non-critical modifications. (off)
        public const string WalLogHints = "wal_log_hints";
        // Sets the maximum interval between WAL receiver status reports to the primary. (10s)
        public const string WalReceiverStatusInterval = "wal_receiver_status_interval";
        // Sets the maximum wait time to receive data from the primary. (1min)
        public const string WalReceiverTimeout = "wal_receiver_timeout";
        // Sets the time to wait before retrying to retrieve WAL after a failed attempt. (5s)
        public const string WalRetrieveRetryInterval = "wal_retrieve_retry_interval";
        // Shows the number of pages per write ahead log segment. (16MB)
        public const string WalSegmentSize = "wal_segment_size";
        // Sets the maximum time to wait for WAL replication. (1min)
        public const string WalSenderTimeout = "wal_sender_timeout";
        // Selects the method used for forcing WAL updates to disk. (open_datasync)
        public const string WalSyncMethod = "wal_sync_method";
        // WAL writer sleep time between WAL flushes. (200ms)
        public const string WalWriterDelay = "wal_writer_delay";
        // Sets the maximum memory to be used for query workspaces. (96MB)
        public const string WorkMem = "work_mem";
        // Sets how binary values are to be encoded in XML. (base64)
        public const string Xmlbinary = "xmlbinary";
        // Sets whether XML data in implicit parsing and serialization operations is to be considered as documents or content fragments. (content)
        public const string Xmloption = "xmloption";
        // Continues processing past damaged page headers. (off)
        public const string ZeroDamagedPages = "zero_damaged_pages";

        internal static string GetPropertyName(PostgresPropertyName property)
        {
            switch (property)
            {
                case PostgresPropertyName.AllowSystemTableMods: return AllowSystemTableMods;
                case PostgresPropertyName.ApplicationName: return ApplicationName;
                case PostgresPropertyName.ArchiveCommand: return ArchiveCommand;
                case PostgresPropertyName.ArchiveMode: return ArchiveMode;
                case PostgresPropertyName.ArchiveTimeout: return ArchiveTimeout;
                case PostgresPropertyName.ArrayNulls: return ArrayNulls;
                case PostgresPropertyName.AuthenticationTimeout: return AuthenticationTimeout;
                case PostgresPropertyName.Autovacuum: return Autovacuum;
                case PostgresPropertyName.AutovacuumAnalyzeScaleFactor: return AutovacuumAnalyzeScaleFactor;
                case PostgresPropertyName.AutovacuumAnalyzeThreshold: return AutovacuumAnalyzeThreshold;
                case PostgresPropertyName.AutovacuumFreezeMaxAge: return AutovacuumFreezeMaxAge;
                case PostgresPropertyName.AutovacuumMaxWorkers: return AutovacuumMaxWorkers;
                case PostgresPropertyName.AutovacuumMultixactFreezeMaxAge: return AutovacuumMultixactFreezeMaxAge;
                case PostgresPropertyName.AutovacuumNaptime: return AutovacuumNaptime;
                case PostgresPropertyName.AutovacuumVacuumCostDelay: return AutovacuumVacuumCostDelay;
                case PostgresPropertyName.AutovacuumVacuumCostLimit: return AutovacuumVacuumCostLimit;
                case PostgresPropertyName.AutovacuumVacuumScaleFactor: return AutovacuumVacuumScaleFactor;
                case PostgresPropertyName.AutovacuumVacuumThreshold: return AutovacuumVacuumThreshold;
                case PostgresPropertyName.AutovacuumWorkMem: return AutovacuumWorkMem;
                case PostgresPropertyName.BackslashQuote: return BackslashQuote;
                case PostgresPropertyName.BgwriterDelay: return BgwriterDelay;
                case PostgresPropertyName.BgwriterLruMaxpages: return BgwriterLruMaxpages;
                case PostgresPropertyName.BgwriterLruMultiplier: return BgwriterLruMultiplier;
                case PostgresPropertyName.BlockSize: return BlockSize;
                case PostgresPropertyName.Bonjour: return Bonjour;
                case PostgresPropertyName.BonjourName: return BonjourName;
                case PostgresPropertyName.ByteaOutput: return ByteaOutput;
                case PostgresPropertyName.CheckFunctionBodies: return CheckFunctionBodies;
                case PostgresPropertyName.CheckpointCompletionTarget: return CheckpointCompletionTarget;
                case PostgresPropertyName.CheckpointTimeout: return CheckpointTimeout;
                case PostgresPropertyName.CheckpointWarning: return CheckpointWarning;
                case PostgresPropertyName.ClientEncoding: return ClientEncoding;
                case PostgresPropertyName.ClientMinMessages: return ClientMinMessages;
                case PostgresPropertyName.ClusterName: return ClusterName;
                case PostgresPropertyName.CommitDelay: return CommitDelay;
                case PostgresPropertyName.CommitSiblings: return CommitSiblings;
                case PostgresPropertyName.ConstraintExclusion: return ConstraintExclusion;
                case PostgresPropertyName.CpuIndexTupleCost: return CpuIndexTupleCost;
                case PostgresPropertyName.CpuOperatorCost: return CpuOperatorCost;
                case PostgresPropertyName.CpuTupleCost: return CpuTupleCost;
                case PostgresPropertyName.CursorTupleFraction: return CursorTupleFraction;
                case PostgresPropertyName.DataChecksums: return DataChecksums;
                case PostgresPropertyName.DateStyle: return DateStyle;
                case PostgresPropertyName.DbUserNamespace: return DbUserNamespace;
                case PostgresPropertyName.DeadlockTimeout: return DeadlockTimeout;
                case PostgresPropertyName.DebugAssertions: return DebugAssertions;
                case PostgresPropertyName.DebugPrettyPrint: return DebugPrettyPrint;
                case PostgresPropertyName.DebugPrintParse: return DebugPrintParse;
                case PostgresPropertyName.DebugPrintPlan: return DebugPrintPlan;
                case PostgresPropertyName.DebugPrintRewritten: return DebugPrintRewritten;
                case PostgresPropertyName.DefaultStatisticsTarget: return DefaultStatisticsTarget;
                case PostgresPropertyName.DefaultTablespace: return DefaultTablespace;
                case PostgresPropertyName.DefaultTextSearchConfig: return DefaultTextSearchConfig;
                case PostgresPropertyName.DefaultTransactionDeferrable: return DefaultTransactionDeferrable;
                case PostgresPropertyName.DefaultTransactionIsolation: return DefaultTransactionIsolation;
                case PostgresPropertyName.DefaultTransactionReadOnly: return DefaultTransactionReadOnly;
                case PostgresPropertyName.DefaultWithOids: return DefaultWithOids;
                case PostgresPropertyName.DynamicSharedMemoryType: return DynamicSharedMemoryType;
                case PostgresPropertyName.EffectiveCacheSize: return EffectiveCacheSize;
                case PostgresPropertyName.EffectiveIoConcurrency: return EffectiveIoConcurrency;
                case PostgresPropertyName.EnableBitmapscan: return EnableBitmapscan;
                case PostgresPropertyName.EnableHashagg: return EnableHashagg;
                case PostgresPropertyName.EnableHashjoin: return EnableHashjoin;
                case PostgresPropertyName.EnableIndexonlyscan: return EnableIndexonlyscan;
                case PostgresPropertyName.EnableIndexscan: return EnableIndexscan;
                case PostgresPropertyName.EnableMaterial: return EnableMaterial;
                case PostgresPropertyName.EnableMergejoin: return EnableMergejoin;
                case PostgresPropertyName.EnableNestloop: return EnableNestloop;
                case PostgresPropertyName.EnableSeqscan: return EnableSeqscan;
                case PostgresPropertyName.EnableSort: return EnableSort;
                case PostgresPropertyName.EnableTidscan: return EnableTidscan;
                case PostgresPropertyName.EscapeStringWarning: return EscapeStringWarning;
                case PostgresPropertyName.EventSource: return EventSource;
                case PostgresPropertyName.ExitOnError: return ExitOnError;
                case PostgresPropertyName.ExtraFloatDigits: return ExtraFloatDigits;
                case PostgresPropertyName.FromCollapseLimit: return FromCollapseLimit;
                case PostgresPropertyName.Fsync: return Fsync;
                case PostgresPropertyName.FullPageWrites: return FullPageWrites;
                case PostgresPropertyName.Geqo: return Geqo;
                case PostgresPropertyName.GeqoEffort: return GeqoEffort;
                case PostgresPropertyName.GeqoGenerations: return GeqoGenerations;
                case PostgresPropertyName.GeqoPoolSize: return GeqoPoolSize;
                case PostgresPropertyName.GeqoSeed: return GeqoSeed;
                case PostgresPropertyName.GeqoSelectionBias: return GeqoSelectionBias;
                case PostgresPropertyName.GeqoThreshold: return GeqoThreshold;
                case PostgresPropertyName.GinFuzzySearchLimit: return GinFuzzySearchLimit;
                case PostgresPropertyName.GinPendingListLimit: return GinPendingListLimit;
                case PostgresPropertyName.HotStandby: return HotStandby;
                case PostgresPropertyName.HotStandbyFeedback: return HotStandbyFeedback;
                case PostgresPropertyName.HugePages: return HugePages;
                case PostgresPropertyName.IgnoreChecksumFailure: return IgnoreChecksumFailure;
                case PostgresPropertyName.IgnoreSystemIndexes: return IgnoreSystemIndexes;
                case PostgresPropertyName.IntegerDatetimes: return IntegerDatetimes;
                case PostgresPropertyName.IntervalStyle: return IntervalStyle;
                case PostgresPropertyName.JoinCollapseLimit: return JoinCollapseLimit;
                case PostgresPropertyName.KrbCaseinsUsers: return KrbCaseinsUsers;
                case PostgresPropertyName.LcCollate: return LcCollate;
                case PostgresPropertyName.LcCtype: return LcCtype;
                case PostgresPropertyName.LcMessages: return LcMessages;
                case PostgresPropertyName.LcMonetary: return LcMonetary;
                case PostgresPropertyName.LcNumeric: return LcNumeric;
                case PostgresPropertyName.LcTime: return LcTime;
                case PostgresPropertyName.ListenAddresses: return ListenAddresses;
                case PostgresPropertyName.LoCompatPrivileges: return LoCompatPrivileges;
                case PostgresPropertyName.LocalPreloadLibraries: return LocalPreloadLibraries;
                case PostgresPropertyName.LockTimeout: return LockTimeout;
                case PostgresPropertyName.LogAutovacuumMinDuration: return LogAutovacuumMinDuration;
                case PostgresPropertyName.LogCheckpoints: return LogCheckpoints;
                case PostgresPropertyName.LogConnections: return LogConnections;
                case PostgresPropertyName.LogDestination: return LogDestination;
                case PostgresPropertyName.LogDisconnections: return LogDisconnections;
                case PostgresPropertyName.LogDuration: return LogDuration;
                case PostgresPropertyName.LogErrorVerbosity: return LogErrorVerbosity;
                case PostgresPropertyName.LogExecutorStats: return LogExecutorStats;
                case PostgresPropertyName.LogFileMode: return LogFileMode;
                case PostgresPropertyName.LogHostname: return LogHostname;
                case PostgresPropertyName.LogLinePrefix: return LogLinePrefix;
                case PostgresPropertyName.LogLockWaits: return LogLockWaits;
                case PostgresPropertyName.LogMinDurationStatement: return LogMinDurationStatement;
                case PostgresPropertyName.LogMinErrorStatement: return LogMinErrorStatement;
                case PostgresPropertyName.LogMinMessages: return LogMinMessages;
                case PostgresPropertyName.LogParserStats: return LogParserStats;
                case PostgresPropertyName.LogPlannerStats: return LogPlannerStats;
                case PostgresPropertyName.LogReplicationCommands: return LogReplicationCommands;
                case PostgresPropertyName.LogRotationAge: return LogRotationAge;
                case PostgresPropertyName.LogRotationSize: return LogRotationSize;
                case PostgresPropertyName.LogStatement: return LogStatement;
                case PostgresPropertyName.LogStatementStats: return LogStatementStats;
                case PostgresPropertyName.LogTempFiles: return LogTempFiles;
                case PostgresPropertyName.LogTimezone: return LogTimezone;
                case PostgresPropertyName.LogTruncateOnRotation: return LogTruncateOnRotation;
                case PostgresPropertyName.LoggingCollector: return LoggingCollector;
                case PostgresPropertyName.MaintenanceWorkMem: return MaintenanceWorkMem;
                case PostgresPropertyName.MaxConnections: return MaxConnections;
                case PostgresPropertyName.MaxFilesPerProcess: return MaxFilesPerProcess;
                case PostgresPropertyName.MaxFunctionArgs: return MaxFunctionArgs;
                case PostgresPropertyName.MaxIdentifierLength: return MaxIdentifierLength;
                case PostgresPropertyName.MaxIndexKeys: return MaxIndexKeys;
                case PostgresPropertyName.MaxLocksPerTransaction: return MaxLocksPerTransaction;
                case PostgresPropertyName.MaxPredLocksPerTransaction: return MaxPredLocksPerTransaction;
                case PostgresPropertyName.MaxPreparedTransactions: return MaxPreparedTransactions;
                case PostgresPropertyName.MaxReplicationSlots: return MaxReplicationSlots;
                case PostgresPropertyName.MaxStackDepth: return MaxStackDepth;
                case PostgresPropertyName.MaxStandbyArchiveDelay: return MaxStandbyArchiveDelay;
                case PostgresPropertyName.MaxStandbyStreamingDelay: return MaxStandbyStreamingDelay;
                case PostgresPropertyName.MaxWalSenders: return MaxWalSenders;
                case PostgresPropertyName.MaxWalSize: return MaxWalSize;
                case PostgresPropertyName.MaxWorkerProcesses: return MaxWorkerProcesses;
                case PostgresPropertyName.MinWalSize: return MinWalSize;
                case PostgresPropertyName.OperatorPrecedenceWarning: return OperatorPrecedenceWarning;
                case PostgresPropertyName.PasswordEncryption: return PasswordEncryption;
                case PostgresPropertyName.Port: return Port;
                case PostgresPropertyName.PostAuthDelay: return PostAuthDelay;
                case PostgresPropertyName.PreAuthDelay: return PreAuthDelay;
                case PostgresPropertyName.QuoteAllIdentifiers: return QuoteAllIdentifiers;
                case PostgresPropertyName.RandomPageCost: return RandomPageCost;
                case PostgresPropertyName.RestartAfterCrash: return RestartAfterCrash;
                case PostgresPropertyName.RowSecurity: return RowSecurity;
                case PostgresPropertyName.SearchPath: return SearchPath;
                case PostgresPropertyName.SegmentSize: return SegmentSize;
                case PostgresPropertyName.SeqPageCost: return SeqPageCost;
                case PostgresPropertyName.ServerEncoding: return ServerEncoding;
                case PostgresPropertyName.ServerVersion: return ServerVersion;
                case PostgresPropertyName.ServerVersionNum: return ServerVersionNum;
                case PostgresPropertyName.SessionReplicationRole: return SessionReplicationRole;
                case PostgresPropertyName.SharedBuffers: return SharedBuffers;
                case PostgresPropertyName.SqlInheritance: return SqlInheritance;
                case PostgresPropertyName.Ssl: return Ssl;
                case PostgresPropertyName.SslCaFile: return SslCaFile;
                case PostgresPropertyName.SslCertFile: return SslCertFile;
                case PostgresPropertyName.SslCrlFile: return SslCrlFile;
                case PostgresPropertyName.SslKeyFile: return SslKeyFile;
                case PostgresPropertyName.SslPreferServerCiphers: return SslPreferServerCiphers;
                case PostgresPropertyName.StandardConformingStrings: return StandardConformingStrings;
                case PostgresPropertyName.StatementTimeout: return StatementTimeout;
                case PostgresPropertyName.SuperuserReservedConnections: return SuperuserReservedConnections;
                case PostgresPropertyName.SynchronizeSeqscans: return SynchronizeSeqscans;
                case PostgresPropertyName.SynchronousCommit: return SynchronousCommit;
                case PostgresPropertyName.SynchronousStandbyNames: return SynchronousStandbyNames;
                case PostgresPropertyName.SyslogFacility: return SyslogFacility;
                case PostgresPropertyName.SyslogIdent: return SyslogIdent;
                case PostgresPropertyName.TcpKeepalivesCount: return TcpKeepalivesCount;
                case PostgresPropertyName.TcpKeepalivesIdle: return TcpKeepalivesIdle;
                case PostgresPropertyName.TcpKeepalivesInterval: return TcpKeepalivesInterval;
                case PostgresPropertyName.TempBuffers: return TempBuffers;
                case PostgresPropertyName.TempFileLimit: return TempFileLimit;
                case PostgresPropertyName.TempTablespaces: return TempTablespaces;
                case PostgresPropertyName.TimeZone: return TimeZone;
                case PostgresPropertyName.TimezoneAbbreviations: return TimezoneAbbreviations;
                case PostgresPropertyName.TraceNotify: return TraceNotify;
                case PostgresPropertyName.TraceRecoveryMessages: return TraceRecoveryMessages;
                case PostgresPropertyName.TraceSort: return TraceSort;
                case PostgresPropertyName.TrackActivities: return TrackActivities;
                case PostgresPropertyName.TrackActivityQuerySize: return TrackActivityQuerySize;
                case PostgresPropertyName.TrackCommitTimestamp: return TrackCommitTimestamp;
                case PostgresPropertyName.TrackCounts: return TrackCounts;
                case PostgresPropertyName.TrackFunctions: return TrackFunctions;
                case PostgresPropertyName.TrackIoTiming: return TrackIoTiming;
                case PostgresPropertyName.TransactionDeferrable: return TransactionDeferrable;
                case PostgresPropertyName.TransactionIsolation: return TransactionIsolation;
                case PostgresPropertyName.TransactionReadOnly: return TransactionReadOnly;
                case PostgresPropertyName.TransformNullEquals: return TransformNullEquals;
                case PostgresPropertyName.UnixSocketGroup: return UnixSocketGroup;
                case PostgresPropertyName.UnixSocketPermissions: return UnixSocketPermissions;
                case PostgresPropertyName.UpdateProcessTitle: return UpdateProcessTitle;
                case PostgresPropertyName.VacuumCostDelay: return VacuumCostDelay;
                case PostgresPropertyName.VacuumCostLimit: return VacuumCostLimit;
                case PostgresPropertyName.VacuumCostPageDirty: return VacuumCostPageDirty;
                case PostgresPropertyName.VacuumCostPageHit: return VacuumCostPageHit;
                case PostgresPropertyName.VacuumCostPageMiss: return VacuumCostPageMiss;
                case PostgresPropertyName.VacuumDeferCleanupAge: return VacuumDeferCleanupAge;
                case PostgresPropertyName.VacuumFreezeMinAge: return VacuumFreezeMinAge;
                case PostgresPropertyName.VacuumFreezeTableAge: return VacuumFreezeTableAge;
                case PostgresPropertyName.VacuumMultixactFreezeMinAge: return VacuumMultixactFreezeMinAge;
                case PostgresPropertyName.VacuumMultixactFreezeTableAge: return VacuumMultixactFreezeTableAge;
                case PostgresPropertyName.WalBlockSize: return WalBlockSize;
                case PostgresPropertyName.WalBuffers: return WalBuffers;
                case PostgresPropertyName.WalCompression: return WalCompression;
                case PostgresPropertyName.WalKeepSegments: return WalKeepSegments;
                case PostgresPropertyName.WalLevel: return WalLevel;
                case PostgresPropertyName.WalLogHints: return WalLogHints;
                case PostgresPropertyName.WalReceiverStatusInterval: return WalReceiverStatusInterval;
                case PostgresPropertyName.WalReceiverTimeout: return WalReceiverTimeout;
                case PostgresPropertyName.WalRetrieveRetryInterval: return WalRetrieveRetryInterval;
                case PostgresPropertyName.WalSegmentSize: return WalSegmentSize;
                case PostgresPropertyName.WalSenderTimeout: return WalSenderTimeout;
                case PostgresPropertyName.WalSyncMethod: return WalSyncMethod;
                case PostgresPropertyName.WalWriterDelay: return WalWriterDelay;
                case PostgresPropertyName.WorkMem: return WorkMem;
                case PostgresPropertyName.Xmlbinary: return Xmlbinary;
                case PostgresPropertyName.Xmloption: return Xmloption;
                case PostgresPropertyName.ZeroDamagedPages: return ZeroDamagedPages;
            }

            throw new ArgumentOutOfRangeException(
                nameof(property), property,
                "Unknown Posrgres property. Must be a member of PostgresPropertyName.");
        }
    }

    public enum PostgresPropertyName
    {
        /// <summary>Allows modifications of the structure of system tables. (off)</summary>
        AllowSystemTableMods,
        /// <summary>Sets the application name to be reported in statistics and logs. (psql)</summary>
        ApplicationName,
        /// <summary>Sets the shell command that will be called to archive a WAL file. ((disabled))</summary>
        ArchiveCommand,
        /// <summary>Allows archiving of WAL files using archive_command. (off)</summary>
        ArchiveMode,
        /// <summary>Forces a switch to the next xlog file if a new file has not been started within N seconds. (0)</summary>
        ArchiveTimeout,
        /// <summary>Enable input of NULL elements in arrays. (on)</summary>
        ArrayNulls,
        /// <summary>Sets the maximum allowed time to complete client authentication. (1min)</summary>
        AuthenticationTimeout,
        /// <summary>Starts the autovacuum subprocess. (on)</summary>
        Autovacuum,
        /// <summary>Number of tuple inserts, updates, or deletes prior to analyze as a fraction of reltuples. (0.1)</summary>
        AutovacuumAnalyzeScaleFactor,
        /// <summary>Minimum number of tuple inserts, updates, or deletes prior to analyze. (50)</summary>
        AutovacuumAnalyzeThreshold,
        /// <summary>Age at which to autovacuum a table to prevent transaction ID wraparound. (200000000)</summary>
        AutovacuumFreezeMaxAge,
        /// <summary>Sets the maximum number of simultaneously running autovacuum worker processes. (3)</summary>
        AutovacuumMaxWorkers,
        /// <summary>Multixact age at which to autovacuum a table to prevent multixact wraparound. (400000000)</summary>
        AutovacuumMultixactFreezeMaxAge,
        /// <summary>Time to sleep between autovacuum runs. (1min)</summary>
        AutovacuumNaptime,
        /// <summary>Vacuum cost delay in milliseconds, for autovacuum. (20ms)</summary>
        AutovacuumVacuumCostDelay,
        /// <summary>Vacuum cost amount available before napping, for autovacuum. (-1)</summary>
        AutovacuumVacuumCostLimit,
        /// <summary>Number of tuple updates or deletes prior to vacuum as a fraction of reltuples. (0.2)</summary>
        AutovacuumVacuumScaleFactor,
        /// <summary>Minimum number of tuple updates or deletes prior to vacuum. (50)</summary>
        AutovacuumVacuumThreshold,
        /// <summary>Sets the maximum memory to be used by each autovacuum worker process. (-1)</summary>
        AutovacuumWorkMem,
        /// <summary>Sets whether "\'" is allowed in string literals. (safe_encoding)</summary>
        BackslashQuote,
        /// <summary>Background writer sleep time between rounds. (200ms)</summary>
        BgwriterDelay,
        /// <summary>Background writer maximum number of LRU pages to flush per round. (100)</summary>
        BgwriterLruMaxpages,
        /// <summary>Multiple of the average buffer usage to free per round. (2)</summary>
        BgwriterLruMultiplier,
        /// <summary>Shows the size of a disk block. (8192)</summary>
        BlockSize,
        /// <summary>Enables advertising the server via Bonjour. (off)</summary>
        Bonjour,
        /// <summary>Sets the Bonjour service name. ()</summary>
        BonjourName,
        /// <summary>Sets the output format for bytea. (hex)</summary>
        ByteaOutput,
        /// <summary>Check function bodies during CREATE FUNCTION. (on)</summary>
        CheckFunctionBodies,
        /// <summary>Time spent flushing dirty buffers during checkpoint, as fraction of checkpoint interval. (0.9)</summary>
        CheckpointCompletionTarget,
        /// <summary>Sets the maximum time between automatic WAL checkpoints. (5min)</summary>
        CheckpointTimeout,
        /// <summary>Enables warnings if checkpoint segments are filled more frequently than this. (30s)</summary>
        CheckpointWarning,
        /// <summary>Sets the client's character set encoding. (UTF8)</summary>
        ClientEncoding,
        /// <summary>Sets the message levels that are sent to the client. (notice)</summary>
        ClientMinMessages,
        /// <summary>Sets the name of the cluster, which is included in the process title. ()</summary>
        ClusterName,
        /// <summary>Sets the delay in microseconds between transaction commit and flushing WAL to disk. (0)</summary>
        CommitDelay,
        /// <summary>Sets the minimum concurrent open transactions before performing commit_delay. (5)</summary>
        CommitSiblings,
        /// <summary>Enables the planner to use constraints to optimize queries. (partition)</summary>
        ConstraintExclusion,
        /// <summary>Sets the planner's estimate of the cost of processing each index entry during an index scan. (0.005)</summary>
        CpuIndexTupleCost,
        /// <summary>Sets the planner's estimate of the cost of processing each operator or function call. (0.0025)</summary>
        CpuOperatorCost,
        /// <summary>Sets the planner's estimate of the cost of processing each tuple (row). (0.01)</summary>
        CpuTupleCost,
        /// <summary>Sets the planner's estimate of the fraction of a cursor's rows that will be retrieved. (0.1)</summary>
        CursorTupleFraction,
        /// <summary>Shows whether data checksums are turned on for this cluster. (off)</summary>
        DataChecksums,
        /// <summary>Sets the display format for date and time values. (ISO, MDY)</summary>
        DateStyle,
        /// <summary>Enables per-database user names. (off)</summary>
        DbUserNamespace,
        /// <summary>Sets the time to wait on a lock before checking for deadlock. (1s)</summary>
        DeadlockTimeout,
        /// <summary>Shows whether the running server has assertion checks enabled. (off)</summary>
        DebugAssertions,
        /// <summary>Indents parse and plan tree displays. (on)</summary>
        DebugPrettyPrint,
        /// <summary>Logs each query's parse tree. (off)</summary>
        DebugPrintParse,
        /// <summary>Logs each query's execution plan. (off)</summary>
        DebugPrintPlan,
        /// <summary>Logs each query's rewritten parse tree. (off)</summary>
        DebugPrintRewritten,
        /// <summary>Sets the default statistics target. (100)</summary>
        DefaultStatisticsTarget,
        /// <summary>Sets the default tablespace to create tables and indexes in. ()</summary>
        DefaultTablespace,
        /// <summary>Sets default text search configuration. (pg_catalog.english)</summary>
        DefaultTextSearchConfig,
        /// <summary>Sets the default deferrable status of new transactions. (off)</summary>
        DefaultTransactionDeferrable,
        /// <summary>Sets the transaction isolation level of each new transaction. (read committed)</summary>
        DefaultTransactionIsolation,
        /// <summary>Sets the default read-only status of new transactions. (off)</summary>
        DefaultTransactionReadOnly,
        /// <summary>Create new tables with OIDs by default. (off)</summary>
        DefaultWithOids,
        /// <summary>Selects the dynamic shared memory implementation used. (posix)</summary>
        DynamicSharedMemoryType,
        /// <summary>Sets the planner's assumption about the size of the disk cache. (10GB)</summary>
        EffectiveCacheSize,
        /// <summary>Number of simultaneous requests that can be handled efficiently by the disk subsystem. (0)</summary>
        EffectiveIoConcurrency,
        /// <summary>Enables the planner's use of bitmap-scan plans. (on)</summary>
        EnableBitmapscan,
        /// <summary>Enables the planner's use of hashed aggregation plans. (on)</summary>
        EnableHashagg,
        /// <summary>Enables the planner's use of hash join plans. (on)</summary>
        EnableHashjoin,
        /// <summary>Enables the planner's use of index-only-scan plans. (on)</summary>
        EnableIndexonlyscan,
        /// <summary>Enables the planner's use of index-scan plans. (on)</summary>
        EnableIndexscan,
        /// <summary>Enables the planner's use of materialization. (on)</summary>
        EnableMaterial,
        /// <summary>Enables the planner's use of merge join plans. (on)</summary>
        EnableMergejoin,
        /// <summary>Enables the planner's use of nested-loop join plans. (on)</summary>
        EnableNestloop,
        /// <summary>Enables the planner's use of sequential-scan plans. (on)</summary>
        EnableSeqscan,
        /// <summary>Enables the planner's use of explicit sort steps. (on)</summary>
        EnableSort,
        /// <summary>Enables the planner's use of TID scan plans. (on)</summary>
        EnableTidscan,
        /// <summary>Warn about backslash escapes in ordinary string literals. (on)</summary>
        EscapeStringWarning,
        /// <summary>Sets the application name used to identify PostgreSQL messages in the event log. (PostgreSQL)</summary>
        EventSource,
        /// <summary>Terminate session on any error. (off)</summary>
        ExitOnError,
        /// <summary>Sets the number of digits displayed for floating-point values. (0)</summary>
        ExtraFloatDigits,
        /// <summary>Sets the FROM-list size beyond which subqueries are not collapsed. (8)</summary>
        FromCollapseLimit,
        /// <summary>Forces synchronization of updates to disk. (on)</summary>
        Fsync,
        /// <summary>Writes full pages to WAL when first modified after a checkpoint. (on)</summary>
        FullPageWrites,
        /// <summary>Enables genetic query optimization. (on)</summary>
        Geqo,
        /// <summary>GEQO: effort is used to set the default for other GEQO parameters. (5)</summary>
        GeqoEffort,
        /// <summary>GEQO: number of iterations of the algorithm. (0)</summary>
        GeqoGenerations,
        /// <summary>GEQO: number of individuals in the population. (0)</summary>
        GeqoPoolSize,
        /// <summary>GEQO: seed for random path selection. (0)</summary>
        GeqoSeed,
        /// <summary>GEQO: selective pressure within the population. (2)</summary>
        GeqoSelectionBias,
        /// <summary>Sets the threshold of FROM items beyond which GEQO is used. (12)</summary>
        GeqoThreshold,
        /// <summary>Sets the maximum allowed result for exact search by GIN. (0)</summary>
        GinFuzzySearchLimit,
        /// <summary>Sets the maximum size of the pending list for GIN index. (4MB)</summary>
        GinPendingListLimit,
        /// <summary>Allows connections and queries during recovery. (off)</summary>
        HotStandby,
        /// <summary>Allows feedback from a hot standby to the primary that will avoid query conflicts. (off)</summary>
        HotStandbyFeedback,
        /// <summary>Use of huge pages on Linux. (try)</summary>
        HugePages,
        /// <summary>Continues processing after a checksum failure. (off)</summary>
        IgnoreChecksumFailure,
        /// <summary>Disables reading from system indexes. (off)</summary>
        IgnoreSystemIndexes,
        /// <summary>Datetimes are integer based. (on)</summary>
        IntegerDatetimes,
        /// <summary>Sets the display format for interval values. (postgres)</summary>
        IntervalStyle,
        /// <summary>Sets the FROM-list size beyond which JOIN constructs are not flattened. (8)</summary>
        JoinCollapseLimit,
        /// <summary>Sets whether Kerberos and GSSAPI user names should be treated as case-insensitive. (off)</summary>
        KrbCaseinsUsers,
        /// <summary>Shows the collation order locale. (C)</summary>
        LcCollate,
        /// <summary>Shows the character classification and case conversion locale. (C)</summary>
        LcCtype,
        /// <summary>Sets the language in which messages are displayed. (C)</summary>
        LcMessages,
        /// <summary>Sets the locale for formatting monetary amounts. (C)</summary>
        LcMonetary,
        /// <summary>Sets the locale for formatting numbers. (C)</summary>
        LcNumeric,
        /// <summary>Sets the locale for formatting date and time values. (C)</summary>
        LcTime,
        /// <summary>Sets the host name or IP address(es) to listen to. (*)</summary>
        ListenAddresses,
        /// <summary>Enables backward compatibility mode for privilege checks on large objects. (off)</summary>
        LoCompatPrivileges,
        /// <summary>Lists unprivileged shared libraries to preload into each backend. ()</summary>
        LocalPreloadLibraries,
        /// <summary>Sets the maximum allowed duration of any wait for a lock. (0)</summary>
        LockTimeout,
        /// <summary>Sets the minimum execution time above which autovacuum actions will be logged. (-1)</summary>
        LogAutovacuumMinDuration,
        /// <summary>Logs each checkpoint. (off)</summary>
        LogCheckpoints,
        /// <summary>Logs each successful connection. (off)</summary>
        LogConnections,
        /// <summary>Sets the destination for server log output. (stderr)</summary>
        LogDestination,
        /// <summary>Logs end of a session, including duration. (off)</summary>
        LogDisconnections,
        /// <summary>Logs the duration of each completed SQL statement. (off)</summary>
        LogDuration,
        /// <summary>Sets the verbosity of logged messages. (default)</summary>
        LogErrorVerbosity,
        /// <summary>Writes executor performance statistics to the server log. (off)</summary>
        LogExecutorStats,
        /// <summary>Sets the file permissions for log files. (0600)</summary>
        LogFileMode,
        /// <summary>Logs the host name in the connection logs. (off)</summary>
        LogHostname,
        /// <summary>Controls information prefixed to each log line. ()</summary>
        LogLinePrefix,
        /// <summary>Logs long lock waits. (off)</summary>
        LogLockWaits,
        /// <summary>Sets the minimum execution time above which statements will be logged. (-1)</summary>
        LogMinDurationStatement,
        /// <summary>Causes all statements generating error at or above this level to be logged. (error)</summary>
        LogMinErrorStatement,
        /// <summary>Sets the message levels that are logged. (warning)</summary>
        LogMinMessages,
        /// <summary>Writes parser performance statistics to the server log. (off)</summary>
        LogParserStats,
        /// <summary>Writes planner performance statistics to the server log. (off)</summary>
        LogPlannerStats,
        /// <summary>Logs each replication command. (off)</summary>
        LogReplicationCommands,
        /// <summary>Automatic log file rotation will occur after N minutes. (1d)</summary>
        LogRotationAge,
        /// <summary>Automatic log file rotation will occur after N kilobytes. (10MB)</summary>
        LogRotationSize,
        /// <summary>Sets the type of statements logged. (none)</summary>
        LogStatement,
        /// <summary>Writes cumulative performance statistics to the server log. (off)</summary>
        LogStatementStats,
        /// <summary>Log the use of temporary files larger than this number of kilobytes. (-1)</summary>
        LogTempFiles,
        /// <summary>Sets the time zone to use in log messages. (US/Pacific)</summary>
        LogTimezone,
        /// <summary>Truncate existing log files of same name during log rotation. (off)</summary>
        LogTruncateOnRotation,
        /// <summary>Start a subprocess to capture stderr output and/or csvlogs into log files. (off)</summary>
        LoggingCollector,
        /// <summary>Sets the maximum memory to be used for maintenance operations. (1GB)</summary>
        MaintenanceWorkMem,
        /// <summary>Sets the maximum number of concurrent connections. (100)</summary>
        MaxConnections,
        /// <summary>Sets the maximum number of simultaneously open files for each server process. (1000)</summary>
        MaxFilesPerProcess,
        /// <summary>Shows the maximum number of function arguments. (100)</summary>
        MaxFunctionArgs,
        /// <summary>Shows the maximum identifier length. (63)</summary>
        MaxIdentifierLength,
        /// <summary>Shows the maximum number of index keys. (32)</summary>
        MaxIndexKeys,
        /// <summary>Sets the maximum number of locks per transaction. (64)</summary>
        MaxLocksPerTransaction,
        /// <summary>Sets the maximum number of predicate locks per transaction. (64)</summary>
        MaxPredLocksPerTransaction,
        /// <summary>Sets the maximum number of simultaneously prepared transactions. (0)</summary>
        MaxPreparedTransactions,
        /// <summary>Sets the maximum number of simultaneously defined replication slots. (0)</summary>
        MaxReplicationSlots,
        /// <summary>Sets the maximum stack depth, in kilobytes. (2MB)</summary>
        MaxStackDepth,
        /// <summary>Sets the maximum delay before canceling queries when a hot standby server is processing archived WAL data. (30s)</summary>
        MaxStandbyArchiveDelay,
        /// <summary>Sets the maximum delay before canceling queries when a hot standby server is processing streamed WAL data. (30s)</summary>
        MaxStandbyStreamingDelay,
        /// <summary>Sets the maximum number of simultaneously running WAL sender processes. (0)</summary>
        MaxWalSenders,
        /// <summary>Sets the WAL size that triggers a checkpoint. (1GB)</summary>
        MaxWalSize,
        /// <summary>Maximum number of concurrent worker processes. (8)</summary>
        MaxWorkerProcesses,
        /// <summary>Sets the minimum size to shrink the WAL to. (80MB)</summary>
        MinWalSize,
        /// <summary>Emit a warning for constructs that changed meaning since PostgreSQL 9.4. (off)</summary>
        OperatorPrecedenceWarning,
        /// <summary>Encrypt passwords. (on)</summary>
        PasswordEncryption,
        /// <summary>Sets the TCP port the server listens on. (5432)</summary>
        Port,
        /// <summary>Waits N seconds on connection startup after authentication. (0)</summary>
        PostAuthDelay,
        /// <summary>Waits N seconds on connection startup before authentication. (0)</summary>
        PreAuthDelay,
        /// <summary>When generating SQL fragments, quote all identifiers. (off)</summary>
        QuoteAllIdentifiers,
        /// <summary>Sets the planner's estimate of the cost of a nonsequentially fetched disk page. (4)</summary>
        RandomPageCost,
        /// <summary>Reinitialize server after backend crash. (on)</summary>
        RestartAfterCrash,
        /// <summary>Enable row security. (on)</summary>
        RowSecurity,
        /// <summary>Sets the schema search order for names that are not schema-qualified. ("$user", public)</summary>
        SearchPath,
        /// <summary>Shows the number of pages per disk file. (1GB)</summary>
        SegmentSize,
        /// <summary>Sets the planner's estimate of the cost of a sequentially fetched disk page. (1)</summary>
        SeqPageCost,
        /// <summary>Sets the server (database) character set encoding. (UTF8)</summary>
        ServerEncoding,
        /// <summary>Shows the server version. (9.5.4)</summary>
        ServerVersion,
        /// <summary>Shows the server version as an integer. (90504)</summary>
        ServerVersionNum,
        /// <summary>Sets the session's behavior for triggers and rewrite rules. (origin)</summary>
        SessionReplicationRole,
        /// <summary>Sets the number of shared memory buffers used by the server. (3584MB)</summary>
        SharedBuffers,
        /// <summary>Causes subtables to be included by default in various commands. (on)</summary>
        SqlInheritance,
        /// <summary>Enables SSL connections. (off)</summary>
        Ssl,
        /// <summary>Location of the SSL certificate authority file. ()</summary>
        SslCaFile,
        /// <summary>Location of the SSL server certificate file. (server.crt)</summary>
        SslCertFile,
        /// <summary>Location of the SSL certificate revocation list file. ()</summary>
        SslCrlFile,
        /// <summary>Location of the SSL server private key file. (server.key)</summary>
        SslKeyFile,
        /// <summary>Give priority to server ciphersuite order. (on)</summary>
        SslPreferServerCiphers,
        /// <summary>Causes '...' strings to treat backslashes literally. (on)</summary>
        StandardConformingStrings,
        /// <summary>Sets the maximum allowed duration of any statement. (0)</summary>
        StatementTimeout,
        /// <summary>Sets the number of connection slots reserved for superusers. (3)</summary>
        SuperuserReservedConnections,
        /// <summary>Enable synchronized sequential scans. (on)</summary>
        SynchronizeSeqscans,
        /// <summary>Sets the current transaction's synchronization level. (on)</summary>
        SynchronousCommit,
        /// <summary>List of names of potential synchronous standbys. ()</summary>
        SynchronousStandbyNames,
        /// <summary>Sets the syslog "facility" to be used when syslog enabled. (local0)</summary>
        SyslogFacility,
        /// <summary>Sets the program name used to identify PostgreSQL messages in syslog. (postgres)</summary>
        SyslogIdent,
        /// <summary>Maximum number of TCP keepalive retransmits. (8)</summary>
        TcpKeepalivesCount,
        /// <summary>Time between issuing TCP keepalives. (7200)</summary>
        TcpKeepalivesIdle,
        /// <summary>Time between TCP keepalive retransmits. (75)</summary>
        TcpKeepalivesInterval,
        /// <summary>Sets the maximum number of temporary buffers used by each session. (8MB)</summary>
        TempBuffers,
        /// <summary>Limits the total size of all temporary files used by each session. (-1)</summary>
        TempFileLimit,
        /// <summary>Sets the tablespace(s) to use for temporary tables and sort files. ()</summary>
        TempTablespaces,
        /// <summary>Sets the time zone for displaying and interpreting time stamps. (US/Pacific)</summary>
        TimeZone,
        /// <summary>Selects a file of time zone abbreviations. (Default)</summary>
        TimezoneAbbreviations,
        /// <summary>Generates debugging output for LISTEN and NOTIFY. (off)</summary>
        TraceNotify,
        /// <summary>Enables logging of recovery-related debugging information. (log)</summary>
        TraceRecoveryMessages,
        /// <summary>Emit information about resource usage in sorting. (off)</summary>
        TraceSort,
        /// <summary>Collects information about executing commands. (on)</summary>
        TrackActivities,
        /// <summary>Sets the size reserved for pg_stat_activity.query, in bytes. (1024)</summary>
        TrackActivityQuerySize,
        /// <summary>Collects transaction commit time. (off)</summary>
        TrackCommitTimestamp,
        /// <summary>Collects statistics on database activity. (on)</summary>
        TrackCounts,
        /// <summary>Collects function-level statistics on database activity. (none)</summary>
        TrackFunctions,
        /// <summary>Collects timing statistics for database I/O activity. (off)</summary>
        TrackIoTiming,
        /// <summary>Whether to defer a read-only serializable transaction until it can be executed with no possible serialization failures. (off)</summary>
        TransactionDeferrable,
        /// <summary>Sets the current transaction's isolation level. (read committed)</summary>
        TransactionIsolation,
        /// <summary>Sets the current transaction's read-only status. (off)</summary>
        TransactionReadOnly,
        /// <summary>Treats "expr=NULL" as "expr IS NULL". (off)</summary>
        TransformNullEquals,
        /// <summary>Sets the owning group of the Unix-domain socket. ()</summary>
        UnixSocketGroup,
        /// <summary>Sets the access permissions of the Unix-domain socket. (0777)</summary>
        UnixSocketPermissions,
        /// <summary>Updates the process title to show the active SQL command. (on)</summary>
        UpdateProcessTitle,
        /// <summary>Vacuum cost delay in milliseconds. (0)</summary>
        VacuumCostDelay,
        /// <summary>Vacuum cost amount available before napping. (200)</summary>
        VacuumCostLimit,
        /// <summary>Vacuum cost for a page dirtied by vacuum. (20)</summary>
        VacuumCostPageDirty,
        /// <summary>Vacuum cost for a page found in the buffer cache. (1)</summary>
        VacuumCostPageHit,
        /// <summary>Vacuum cost for a page not found in the buffer cache. (10)</summary>
        VacuumCostPageMiss,
        /// <summary>Number of transactions by which VACUUM and HOT cleanup should be deferred, if any. (0)</summary>
        VacuumDeferCleanupAge,
        /// <summary>Minimum age at which VACUUM should freeze a table row. (50000000)</summary>
        VacuumFreezeMinAge,
        /// <summary>Age at which VACUUM should scan whole table to freeze tuples. (150000000)</summary>
        VacuumFreezeTableAge,
        /// <summary>Minimum age at which VACUUM should freeze a MultiXactId in a table row. (5000000)</summary>
        VacuumMultixactFreezeMinAge,
        /// <summary>Multixact age at which VACUUM should scan whole table to freeze tuples. (150000000)</summary>
        VacuumMultixactFreezeTableAge,
        /// <summary>Shows the block size in the write ahead log. (8192)</summary>
        WalBlockSize,
        /// <summary>Sets the number of disk-page buffers in shared memory for WAL. (16MB)</summary>
        WalBuffers,
        /// <summary>Compresses full-page writes written in WAL file. (off)</summary>
        WalCompression,
        /// <summary>Sets the number of WAL files held for standby servers. (0)</summary>
        WalKeepSegments,
        /// <summary>Set the level of information written to the WAL. (minimal)</summary>
        WalLevel,
        /// <summary>Writes full pages to WAL when first modified after a checkpoint, even for a non-critical modifications. (off)</summary>
        WalLogHints,
        /// <summary>Sets the maximum interval between WAL receiver status reports to the primary. (10s)</summary>
        WalReceiverStatusInterval,
        /// <summary>Sets the maximum wait time to receive data from the primary. (1min)</summary>
        WalReceiverTimeout,
        /// <summary>Sets the time to wait before retrying to retrieve WAL after a failed attempt. (5s)</summary>
        WalRetrieveRetryInterval,
        /// <summary>Shows the number of pages per write ahead log segment. (16MB)</summary>
        WalSegmentSize,
        /// <summary>Sets the maximum time to wait for WAL replication. (1min)</summary>
        WalSenderTimeout,
        /// <summary>Selects the method used for forcing WAL updates to disk. (open_datasync)</summary>
        WalSyncMethod,
        /// <summary>WAL writer sleep time between WAL flushes. (200ms)</summary>
        WalWriterDelay,
        /// <summary>Sets the maximum memory to be used for query workspaces. (96MB)</summary>
        WorkMem,
        /// <summary>Sets how binary values are to be encoded in XML. (base64)</summary>
        Xmlbinary,
        /// <summary>Sets whether XML data in implicit parsing and serialization operations is to be considered as documents or content fragments. (content)</summary>
        Xmloption,
        /// <summary>Continues processing past damaged page headers. (off)</summary>
        ZeroDamagedPages
    }
}
