using System.Collections.Generic;
using System.Linq;

namespace Application.Models
{
    public class ServiceResult<T>
    {
        public bool IsSuccess { get; private set; }
        public T? Data { get; private set; }
        public List<string> Errors { get; private set; } = new List<string>();

        private ServiceResult(bool isSuccess, T? data, List<string>? errors)
        {
            IsSuccess = isSuccess;
            Data = data;
            if (errors != null)
            {
                Errors = errors;
            }
        }

        public static ServiceResult<T> Success(T data)
        {
            return new ServiceResult<T>(true, data, null);
        }

        public static ServiceResult<T> Failure(string error)
        {
            return new ServiceResult<T>(false, default, new List<string> { error });
        }

        public static ServiceResult<T> Failure(IEnumerable<string> errors)
        {
            return new ServiceResult<T>(false, default, errors.ToList());
        }
    }

    // Non-generic version for operations without a return value
    public class ServiceResult
    {
        public bool IsSuccess { get; private set; }
        public List<string> Errors { get; private set; } = new List<string>();

        private ServiceResult(bool isSuccess, List<string>? errors)
        {
            IsSuccess = isSuccess;
            if (errors != null)
            {
                Errors = errors;
            }
        }

        public static ServiceResult Success()
        {
            return new ServiceResult(true, null);
        }

        public static ServiceResult Failure(string error)
        {
            return new ServiceResult(false, new List<string> { error });
        }

        public static ServiceResult Failure(IEnumerable<string> errors)
        {
            return new ServiceResult(false, errors.ToList());
        }
    }
}