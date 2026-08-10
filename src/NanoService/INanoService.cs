namespace NanoService;

internal interface INanoService
{
    void Dispatch(byte[] body, INanoCallContext context);
}
