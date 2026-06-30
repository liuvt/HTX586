namespace HTX586CONTRACT.Domain.Enums;

public enum ContractStatus
{
    Draft = 0,
    WaitingCustomerSignature = 1,
    CustomerSigned = 2,
    WaitingDriverConfirmation = 3,
    Completed = 4,
    Cancelled = 5,
    Expired = 6,
    Invalidated = 7
}
