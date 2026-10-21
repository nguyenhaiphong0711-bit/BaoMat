"""Shared resources, immutable versions, review/publication and file boundaries on an isolated DB."""
import json
import urllib.error
import urllib.request
import uuid
from password_reset_smoke import Web, main
from permission_integration import permission_checks
from staff_integration import staff_checks


def resource_checks(state):
    api, web, mongo, passed = (state[k] for k in ('api','web','mongo','passed'))
    teacher = Web(web.base); student = Web(web.base)
    api(teacher,'/api/auth/login',{'email':'teacher@lms.com','password':'Teacher@123'})
    api(student,'/api/auth/login',{'email':'student@lms.com','password':'Student@123'})
    staff_id = state['browser_environment']['E2E_STAFF_ID']
    mongo('d.Users.updateOne({_id:ObjectId("'+staff_id+'")},{$addToSet:{PermissionCodes:{$each:["resources.view","resources.review"]}}})')
    staff=Web(web.base)
    api(staff,'/api/auth/login',{'email':'staff@example.test','password':'StaffTest@123'})

    def request(client, path, data, method='POST', expected=200, content_type='application/json'):
        token = api(client,'/api/frontend/csrf')['requestToken']
        raw = json.dumps(data).encode() if content_type=='application/json' else data
        req=urllib.request.Request(web.base+path,raw,{'Content-Type':content_type,'RequestVerificationToken':token},method=method)
        try: response=client.opener.open(req,timeout=30)
        except urllib.error.HTTPError as error: response=error
        body=response.read().decode()
        assert response.status==expected,(path,response.status,expected,body[:300])
        return json.loads(body) if body else None
    def upload(client, name='shared-notes.txt', text='Shared resource file'):
        boundary=uuid.uuid4().hex
        raw=(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{name}"\r\nContent-Type: text/plain\r\n\r\n{text}\r\n--{boundary}--\r\n').encode()
        return request(client,'/api/resources/files',raw,content_type='multipart/form-data; boundary='+boundary)
    subjects=api(web,'/api/teachers/'+state['teacher_id']+'/subjects')
    first=next(s for s in subjects if s['code']=='SMTPTEST')['id']
    second=api(web,'/api/subjects',{'departmentId':state['general']['id'],'code':'SHAREDSECOND','name':'Second shared subject'},expected=201)['id']
    api(web,'/api/teachers/'+state['teacher_id']+'/subjects',{'subjectIds':[s['id'] for s in subjects]+[second]},'PUT',204)
    peer=api(web,'/api/users',{'fullName':'Shared resource peer','email':'resource-peer@example.test','password':'LibraryTest@123','role':'Student'},expected=201)
    api(web,'/api/classes',{'name':'Shared resource second class','subjectId':second,'teacherId':state['teacher_id'],'studentIds':[peer['id']]},expected=201)
    peer_client=Web(web.base);api(peer_client,'/api/auth/login',{'email':peer['email'],'password':'LibraryTest@123'})
    outside=Web(web.base);api(outside,'/api/auth/login',{'email':'outside-scope@example.test','password':'StaffTest@123'})
    file=upload(teacher); duplicate=upload(teacher)
    assert file['id']==duplicate['id']
    assert int(mongo('print(d.getCollection("LmsUploads.files").countDocuments({"metadata.purpose":"shared-resource","metadata.ownerId":ObjectId("'+state['teacher_id']+'")}))'))==1
    form={'title':'Shared library reference','description':'Version one','category':'Bài giảng','authorName':'Library author',
        'source':'Original course notes','license':'CC BY 4.0','usageTerms':'Ghi rõ tác giả khi sử dụng.', 'access':'Subjects',
        'externalUrl':'','subjectIds':[first,second],'fileIds':[file['id']]}
    r=request(teacher,'/api/resources',form,expected=201);rid=r['id']
    assert r['state']=='Draft' and len(r['subjectIds'])==2 and r['version']==1
    file_url=f'/api/resources/{rid}/versions/1/files/{file["id"]}'
    for client in (student,peer_client,outside):
        api(client,'/api/resources/'+rid,expected=404);assert client.request(file_url)[0]==404
    assert not api(student,'/api/resources?q=Shared%20library')['items']
    request(teacher,f'/api/resources/{rid}/publish',{'revision':r['revision']},expected=400)
    request(student,'/api/resources',form,expected=403)
    request(teacher,'/api/resources',dict(form,externalUrl='javascript:alert(1)'),expected=400)
    request(teacher,'/api/resources',dict(form,fileIds=['000000000000000000000001']),expected=400)
    request(teacher,'/api/resources',dict(form,fileIds=[]),expected=400)
    request(teacher,'/api/resources',dict(form,fileIds=[file['id']]*6),expected=400)
    injected=request(teacher,'/api/resources',dict(form,state='Published',publishedVersion=1,ownerId=state['student_id']),expected=201)
    assert injected['state']=='Draft' and injected['canSubmit']
    passed('shared resources require valid assigned subjects, safe links and owned files; duplicate uploads reuse the same physical file')

    r=request(teacher,f'/api/resources/{rid}/submit',{'revision':r['revision']})
    request(teacher,f'/api/resources/{rid}/review',{'revision':r['revision'],'approve':True},expected=403)
    request(web,f'/api/resources/{rid}/review',{'revision':r['revision']-1,'approve':True},expected=409)
    request(teacher,'/api/resources/'+rid,dict(form,revision=r['revision']),method='PUT',expected=400)
    mongo('d.Users.updateOne({_id:ObjectId("'+staff_id+'")},{$unset:{DepartmentId:""}})')
    request(staff,f'/api/resources/{rid}/review',{'revision':r['revision'],'approve':True},expected=403)
    mongo('d.Users.updateOne({_id:ObjectId("'+staff_id+'")},{$set:{DepartmentId:ObjectId("'+state['general']['id']+'")}})')
    r=request(staff,f'/api/resources/{rid}/review',{'revision':r['revision'],'approve':True,'comment':'Checked source and license'})
    assert r['state']=='Approved' and r['history'][0]['reviewerName']=='Staff integration'
    assert student.request(file_url)[0]==404
    r=request(teacher,f'/api/resources/{rid}/publish',{'revision':r['revision']})
    for client in (student,peer_client):
        view=api(client,'/api/resources/'+rid)
        assert view['state']=='Published' and view['history']==[] and not view['canEdit']
        assert client.request(file_url)==(200,'Shared resource file')
    api(outside,'/api/resources/'+rid,expected=404)
    assert 'Shared resource file' not in teacher.request('/File/Download/'+file['id'])[1]
    mongo('d.Users.updateOne({_id:ObjectId("'+state['student_id']+'")},{$pull:{PermissionCodes:"resources.view"}})')
    assert student.request(file_url)[0]==403
    mongo('d.Users.updateOne({_id:ObjectId("'+state['student_id']+'")},{$addToSet:{PermissionCodes:"resources.view"}})')
    assert student.request(file_url)[0]==200
    # Existing upload cleanup must not remove a file linked from resource versions.
    token=api(teacher,'/api/frontend/csrf')['requestToken']
    req=urllib.request.Request(web.base+'/Upload/Revert',file['id'].encode(),{'RequestVerificationToken':token},method='DELETE')
    assert teacher.opener.open(req).status==204
    assert student.request(file_url)[0]==200
    passed('review is mandatory, authors cannot self-review, stale reviews fail, and one approved file serves two subjects without exposing drafts')

    revised=dict(form,title='Shared library revised',description='Version two',revision=r['revision'])
    r=request(teacher,'/api/resources/'+rid,revised,method='PUT')
    assert r['version']==2 and r['state']=='Draft'
    assert api(student,'/api/resources/'+rid)['title']==form['title']
    api(student,f'/api/resources/{rid}/versions/2',expected=404)
    assert student.request(f'/api/resources/{rid}/versions/2/files/{file["id"]}')[0]==404
    r=request(teacher,f'/api/resources/{rid}/submit',{'revision':r['revision']})
    request(staff,f'/api/resources/{rid}/review',{'revision':r['revision'],'approve':False},expected=400)
    r=request(staff,f'/api/resources/{rid}/review',{'revision':r['revision'],'approve':False,'comment':'Please clarify the source'})
    assert r['state']=='Rejected'
    revised['revision']=r['revision'];revised['access']='School'
    r=request(teacher,'/api/resources/'+rid,revised,method='PUT')
    assert r['version']==3 and any(v['reviewComment']=='Please clarify the source' for v in r['history'])
    r=request(teacher,f'/api/resources/{rid}/submit',{'revision':r['revision']})
    r=request(web,f'/api/resources/{rid}/review',{'revision':r['revision'],'approve':True})
    r=request(teacher,f'/api/resources/{rid}/publish',{'revision':r['revision']})
    assert api(outside,'/api/resources/'+rid)['version']==3
    api(student,f'/api/resources/{rid}/versions/1',expected=404)
    assert student.request(file_url)[0]==404
    assert api(teacher,f'/api/resources/{rid}/versions/1')['title']==form['title']
    passed('new versions preserve the published snapshot and review history; rejected revisions need changes and older versions stay private after replacement')

    admin=request(web,'/api/resources',dict(form,title='Admin own draft',fileIds=[],externalUrl='https://example.com/notes'),expected=201)
    admin=request(web,f'/api/resources/{admin["id"]}/submit',{'revision':admin['revision']})
    request(web,f'/api/resources/{admin["id"]}/review',{'revision':admin['revision'],'approve':True},expected=403)
    foreign_subject=mongo('print(d.Subjects.findOne({Code:"STAFFOTHER"})._id.toString())')
    request(teacher,'/api/resources',dict(form,subjectIds=[foreign_subject]),expected=400)
    foreign_author=Web(web.base)
    api(foreign_author,'/api/auth/login',{'email':'staff-other-teacher@example.test','password':'StaffTest@123'})
    request(foreign_author,'/api/resources',dict(form,subjectIds=[foreign_subject]),expected=400)
    own_file=upload(foreign_author)
    assert own_file['id']!=file['id'], 'Deduplication disclosed/reused another owner file'
    request(foreign_author,'/api/resources/'+rid,dict(form,subjectIds=[foreign_subject],fileIds=[own_file['id']],revision=r['revision']),method='PUT',expected=403)
    assert Web(web.base).request(f'/api/resources/{rid}/versions/3/files/{file["id"]}')[0]==401
    foreign=request(web,'/api/resources',dict(form,title='Cross department review',subjectIds=[first,foreign_subject],fileIds=[],externalUrl='https://example.com/cross'),expected=201)
    foreign=request(web,f'/api/resources/{foreign["id"]}/submit',{'revision':foreign['revision']})
    request(staff,f'/api/resources/{foreign["id"]}/review',{'revision':foreign['revision'],'approve':True},expected=403)
    request(staff,f'/api/resources/{rid}/archive',{'revision':r['revision']},expected=403)
    r=request(teacher,f'/api/resources/{rid}/archive',{'revision':r['revision']})
    api(student,'/api/resources/'+rid,expected=404)
    assert outside.request(f'/api/resources/{rid}/versions/3/files/{file["id"]}')[0]==404
    assert api(teacher,'/api/resources/'+rid)['isArchived']
    passed('school-wide access requires publication; archive revokes files; even Admin cannot self-approve and Staff cannot review across departments')
    state['browser_environment'].update(E2E_RESOURCE_SUBJECT_ID=first,E2E_RESOURCE_SECOND_ID=second)


def extra_checks(state):
    staff_checks(state)
    resource_checks(state)

if __name__=='__main__':
    main(lambda state: permission_checks(state,extra_checks))
