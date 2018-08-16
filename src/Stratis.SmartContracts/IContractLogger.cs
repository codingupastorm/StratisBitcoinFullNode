namespace Stratis.SmartContracts
{
    public interface IContractLogger
    {
        void Log<T>(T toLog) where T : struct;
    }
}
