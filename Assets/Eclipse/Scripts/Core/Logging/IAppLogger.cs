namespace Eclipse.Core
{
    /// <summary>
    /// 로깅 추상화. 구현체(콘솔·파일 등)를 DI로 교체할 수 있게 한다.
    /// </summary>
    public interface IAppLogger
    {
        void Log(string message);
    }
}