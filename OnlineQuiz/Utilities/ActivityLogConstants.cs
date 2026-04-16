namespace OnlineQuiz.Utilities
{
    public static class ActivityLogConstants
    {
        // Action Types
        public static class Actions
        {
            public const string CREATE = "CREATE";
            public const string UPDATE = "UPDATE";
            public const string DELETE = "DELETE";
            public const string LOGIN = "LOGIN";
            public const string LOGOUT = "LOGOUT";
            public const string PUBLISH = "PUBLISH";
            public const string UNPUBLISH = "UNPUBLISH";
            public const string ENROLL = "ENROLL";
            public const string UNENROLL = "UNENROLL";
            public const string SUBMIT = "SUBMIT";
            public const string GRADE = "GRADE";
            public const string EXPORT = "EXPORT";
            public const string IMPORT = "IMPORT";
            public const string ARCHIVE = "ARCHIVE";
            public const string RESTORE = "RESTORE";
            public const string APPROVE = "APPROVE";
            public const string REJECT = "REJECT";
            public const string VERIFY = "VERIFY";

            public static readonly string[] All = new[]
            {
                CREATE, UPDATE, DELETE,
                LOGIN, LOGOUT,
                PUBLISH, UNPUBLISH,
                ENROLL, UNENROLL,
                SUBMIT, GRADE,
                EXPORT, IMPORT,
                ARCHIVE, RESTORE,
                APPROVE, REJECT,
                VERIFY
            };
        }

        // Entity Types
        public static class Entities
        {
            public const string User = "User";
            public const string Teacher = "Teacher";
            public const string Student = "Student";
            public const string Course = "Course";
            public const string Enrollment = "Enrollment";
            public const string Quiz = "Quiz";
            public const string Question = "Question";
            public const string Choice = "Choice";
            public const string Attempt = "Attempt";
            public const string AttemptAnswer = "AttemptAnswer";
            public const string Notification = "Notification";
            public const string System = "System";
            public const string Auth = "Auth";
            public const string Biometric = "Biometric";

            public static readonly string[] All = new[]
            {
                User, Teacher, Student,
                Course, Enrollment,
                Quiz, Question, Choice,
                Attempt, AttemptAnswer,
                Notification,
                System, Auth, Biometric
            };
        }
    }
}
