export type Role = "Admin" | "Teacher" | "Student";
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
