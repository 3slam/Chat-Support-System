namespace ChatSupport.Models;

/// <summary>The four service categories the customer can pick from the bot flow.</summary>
public enum ChatCategory
{
    Flight = 0,
    Hotel = 1,
    Visa = 2,
    Activity = 3
}

/// <summary>Category-specific sub-options ( استفسار عام | لدي مشكلة مع الحجز ).</summary>
public enum SubOption
{
    GeneralInquiry = 0,   // استفسار عام
    BookingProblem = 1    // لدي مشكلة مع الحجز
}

/// <summary>Lifecycle of a conversation.</summary>
public enum ConversationStatus
{
    Bot = 0,        // still inside the pre-chat bot flow
    Waiting = 1,    // bot flow finished, sitting in the inbox unassigned
    Assigned = 2,   // an agent picked it up
    Closed = 3      // admin closed it
}

/// <summary>Who authored a given message.</summary>
public enum SenderType
{
    Customer = 0,
    Bot = 1,
    Agent = 2
}

/// <summary>Role of a back-office user.</summary>
public enum AdminRole
{
    Agent = 0,
    Admin = 1
}
