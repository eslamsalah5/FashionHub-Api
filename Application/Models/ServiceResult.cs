using System.Collections.Generic;
using System.Linq;

namespace Application.Models
{
    /// <summary>
    /// Defines the type of error returned by a service operation.
    /// Used by controllers to map errors to appropriate HTTP status codes.
    /// </summary>
    public enum ServiceErrorType
    {
        None,
        NotFound,
        Validation,
        Conflict,
        Unauthorized,
        General
    }

    public class ServiceResult<T>
    {
        public bool IsSuccess { get; private set; }
        public T? Data { get; private set; }
        public List<string> Errors { get; private set; } = new List<string>();
        public ServiceErrorType ErrorType { get; private set; } = ServiceErrorType.None;

        private ServiceResult(bool isSuccess, T? data, List<string>? errors, ServiceErrorType errorType = ServiceErrorType.None)
        {
            IsSuccess = isSuccess;
            Data = data;
            ErrorType = errorType;
            if (errors != null)
            {
                Errors = errors;
            }
        }

        public static ServiceResult<T> Success(T data)
            => new ServiceResult<T>(true, data, null);

        public static ServiceResult<T> Failure(string error, ServiceErrorType errorType = ServiceErrorType.General)
            => new ServiceResult<T>(false, default, new List<string> { error }, errorType);

        public static ServiceResult<T> Failure(IEnumerable<string> errors, ServiceErrorType errorType = ServiceErrorType.General)
            => new ServiceResult<T>(false, default, errors.ToList(), errorType);

        public static ServiceResult<T> NotFound(string error)
            => Failure(error, ServiceErrorType.NotFound);

        public static ServiceResult<T> Conflict(string error)
            => Failure(error, ServiceErrorType.Conflict);

        public static ServiceResult<T> ValidationError(string error)
            => Failure(error, ServiceErrorType.Validation);
    }

    // Non-generic version for operations without a return value
    public class ServiceResult
    {
        public bool IsSuccess { get; private set; }
        public List<string> Errors { get; private set; } = new List<string>();
        public ServiceErrorType ErrorType { get; private set; } = ServiceErrorType.None;

        private ServiceResult(bool isSuccess, List<string>? errors, ServiceErrorType errorType = ServiceErrorType.None)
        {
            IsSuccess = isSuccess;
            ErrorType = errorType;
            if (errors != null)
            {
                Errors = errors;
            }
        }

        public static ServiceResult Success()
            => new ServiceResult(true, null);

        public static ServiceResult Failure(string error, ServiceErrorType errorType = ServiceErrorType.General)
            => new ServiceResult(false, new List<string> { error }, errorType);

        public static ServiceResult Failure(IEnumerable<string> errors, ServiceErrorType errorType = ServiceErrorType.General)
            => new ServiceResult(false, errors.ToList(), errorType);

        public static ServiceResult NotFound(string error)
            => Failure(error, ServiceErrorType.NotFound);

        public static ServiceResult Conflict(string error)
            => Failure(error, ServiceErrorType.Conflict);

        public static ServiceResult ValidationError(string error)
            => Failure(error, ServiceErrorType.Validation);
    }
}