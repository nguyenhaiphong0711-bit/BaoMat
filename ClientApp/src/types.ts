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
