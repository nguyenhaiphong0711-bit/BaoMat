export type Role = "Admin" | "Teacher" | "Student" | "Staff";
export interface Identity {
  id: string;
  fullName: string;
  email: string;
  role: Role;
  permissionCodes: string[];
}
export interface Department {
  id: string;
  code: string;
  name: string;
  description: string;
}
export interface Subject {
  id: string;
  departmentId: string;
  code: string;
  name: string;
  description: string;
}
export interface ClassRoom {
  id: string;
  name: string;
  description: string;
  subjectId: string;
  studentIds: string[];
  enrollmentCapacity: number;
}
export interface ClassSummary {
  id: string;
  name: string;
  description: string;
  subjectName: string;
  teacherName: string;
  enrolledCount: number;
  enrollmentCapacity: number;
}
export interface Lesson {
  attachments: Attachment[];
  classId: string;
  id: string;
  title: string;
  content: string;
  isPublished: boolean;
}
export interface Assignment {
  classId: string;
  attachments: Attachment[];
  id: string;
  title: string;
  description: string;
  dueDate: string;
  isPublished: boolean;
}

export interface Attachment {
  id: string;
  fileName: string;
  length: number;
}
export interface Submission {
  id: string;
  assignmentId: string;
  studentId: string;
  studentName: string | null;
  content: string;
  attachments: Attachment[];
  grade: number | null;
  teacherComment: string;
  submittedAt: string;
  gradedAt: string | null;
}

export const roleLabel: Record<Role, string> = {
  Admin: "Quản trị viên",
  Teacher: "Giảng viên",
  Student: "Sinh viên",
  Staff: "Cán bộ quản lý",
};
export interface StaffStudent {
  id: string;
  fullName: string;
  email: string;
  programName: string;
  currentSemester: number;
  isActive: boolean;
}
export interface StaffStudentsPage {
  departmentName: string;
  items: StaffStudent[];
  total: number;
  page: number;
  pageSize: number;
}
export interface StaffProgress {
  student: StaffStudent;
  courses: {
    className: string;
    subjectName: string;
    assignmentCount: number;
    submittedCount: number;
    gradedCount: number;
    averageGrade: number | null;
  }[];
}

export interface ResourceItem {
  id: string;
  revision: number;
  version: number;
  title: string;
  description: string;
  category: string;
  authorName: string;
  source: string;
  license: string;
  usageTerms: string;
  access: "Subjects" | "School";
  externalUrl: string;
  subjectIds: string[];
  subjectNames: string[];
  files: Attachment[];
  state: string;
  isArchived: boolean;
  canEdit: boolean;
  canReview: boolean;
  canPublish: boolean;
  canSubmit: boolean;
  canArchive: boolean;
  reviewComment: string;
  history: {
    number: number;
    title: string;
    state: string;
    createdAt: string;
    reviewerName: string;
    reviewedAt: string | null;
    reviewComment: string;
  }[];
}
export interface ResourcePage {
  items: ResourceItem[];
  total: number;
  page: number;
  pageSize: number;
}
export interface QuestionItem {
  id: string;
  revision: number;
  subjectId: string;
  subjectName: string;
  topic: string;
  difficulty: "Easy" | "Medium" | "Hard";
  content: string;
  options: { id: string; text: string }[];
  correctOption: number;
  explanation: string;
  state: string;
  isArchived: boolean;
  editorName: string;
  reviewerName: string;
  reviewComment: string;
  canEdit: boolean;
  canSubmit: boolean;
  canReview: boolean;
  canPublish: boolean;
  canArchive: boolean;
}
export interface QuestionPage {
  items: QuestionItem[];
  total: number;
  page: number;
  pageSize: number;
}
export interface QuizSummary {
  id: string;
  revision: number;
  classId: string;
  className: string;
  title: string;
  durationMinutes: number;
  opensAt: string;
  closesAt: string;
  questionCount: number;
  isPublished: boolean;
  isArchived: boolean;
  attemptStatus: "NotStarted" | "InProgress" | "Submitted";
  score: number | null;
}
export interface QuizManage {
  quiz: QuizSummary;
  instructions: string;
  questions: QuestionItem[];
  canEdit: boolean;
  canPublish: boolean;
  canArchive: boolean;
  attempts: {
    studentId: string;
    studentName: string;
    startedAt: string;
    submittedAt: string | null;
    score: number | null;
  }[];
}
export interface QuizStudentDetail {
  quiz: QuizSummary;
  instructions: string;
  canStart: boolean;
  attemptId: string | null;
}
export interface QuizAttempt {
  id: string;
  quizId: string;
  title: string;
  instructions: string;
  startedAt: string;
  expiresAt: string;
  submittedAt: string | null;
  score: number | null;
  questions: {
    content: string;
    options: { id: string; text: string }[];
    correctOption: number | null;
    explanation: string;
  }[];
  answers: number[];
}
export interface SupportTicket {
  id: string;
  revision: number;
  subject: string;
  category: string;
  priority: "Low" | "Normal" | "High";
  status: "Open" | "InProgress" | "Resolved" | "Closed";
  studentName: string;
  departmentName: string;
  assignedStaffName: string;
  assignedStaffId: string | null;
  createdAt: string;
  updatedAt: string;
  canReply: boolean;
  canManage: boolean;
  messages: {
    id: string;
    senderName: string;
    senderRole: Role;
    content: string;
    sentAt: string;
  }[];
}
export interface SupportPage {
  items: SupportTicket[];
  total: number;
  page: number;
  pageSize: number;
}
export interface LearningReport {
  scopeName: string;
  metrics: { key: string; label: string; value: number; unit: string }[];
  supportStatuses: { key: string; label: string; value: number }[];
  recentEvents: { type: string; summary: string; occurredAt: string }[];
}
