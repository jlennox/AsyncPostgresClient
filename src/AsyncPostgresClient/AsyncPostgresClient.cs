using System;
using System.Collections.Generic;

namespace AsyncPostgresClient
{
    public enum PostgresMessageType
    {
        AuthenticationOk,
        AuthenticationKerberosV5,
        AuthenticationCleartextPassword,
        AuthenticationMD5Password,
        AuthenticationSCMCredential,
        AuthenticationGSS,
        AuthenticationSSPI,
        AuthenticationGSSContinue,
        BackendKeyData,
        Bind,
        BindComplete,
        CancelRequest,
        Close,
        CloseComplete,
        CommandComplete,
        CopyData,
        CopyDone,
        CopyFail,
        CopyInResponse,
        CopyOutResponse,
        CopyBothResponse,
        DataRow,
        Describe,
        EmptyQueryResponse,
        ErrorResponse,
        Execute,
        Flush,
        FunctionCall,
        FunctionCallResponse,
        NoData,
        NoticeResponse,
        NotificationResponse,
        ParameterDescription,
        ParameterStatus,
        Parse,
        ParseComplete,
        PasswordMessage,
        PortalSuspended,
        Query,
        ReadyForQuery,
        RowDescription,
        SSLRequest,
        StartupMessage,
        Sync,
        Terminate
    }

    public class AsyncPostgresClient
    {
    }
}
